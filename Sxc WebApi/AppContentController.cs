﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Web.Http.Controllers;
using DotNetNuke.Security;
using DotNetNuke.Web.Api;
using ToSic.Eav.Apps;
using ToSic.Eav.Apps.Interfaces;
using ToSic.Eav.Data.Query;
using ToSic.Eav.Interfaces;
using ToSic.Eav.Security.Permissions;
using ToSic.Eav.WebApi;
using ToSic.SexyContent.Engines;
using ToSic.SexyContent.Environment.Dnn7;
using ToSic.SexyContent.Serializers;
using ToSic.SexyContent.WebApi.Permissions;
using ToSic.SexyContent.WebApi.ToRefactorDeliverCBDataLight;
using Factory = ToSic.Eav.Factory;

namespace ToSic.SexyContent.WebApi
{
    /// <inheritdoc />
    /// <summary>
    /// Direct access to app-content items, simple manipulations etc.
    /// Should check for security at each standard call - to see if the current user may do this
    /// Then we can reduce security access level to anonymous, because each method will do the security check
    /// </summary>
    [AllowAnonymous]
    public class AppContentController : SxcApiControllerBase
	{
	    protected override void Initialize(HttpControllerContext controllerContext)
	    {
	        base.Initialize(controllerContext); // very important!!!
	        Log.Rename("2sApCo");
	    }


        #region Get List / all of a certain content-type
        /// <summary>
        /// Get all Entities of specified Type
        /// </summary>
        [HttpGet]
        [AllowAnonymous]   // will check security internally, so assume no requirements
        public IEnumerable<Dictionary<string, object>> GetEntities(string contentType, string appPath = null, string cultureCode = null)
        {
            Log.Add($"get entities type:{contentType}, path:{appPath}, culture:{cultureCode}");

            // if app-path specified, use that app, otherwise use from context
            var appIdentity = AppFinder.GetAppIdFromPathOrContext(appPath, SxcInstance);

            // 2018-09-19 2dm testing
            var appMan = new AppRuntime(appIdentity, Log);
            var appPerms = appMan.Metadata.Get(Eav.Constants.MetadataForApp, Eav.Constants.PermissionTypeName);

            throw new Exception("2dm working on this - trying to get permission check to work without initializing app!");

            var permCheck = new MultiPermissionsTypes(SxcInstance, appIdentity.AppId, contentType, Log);
            if (!permCheck.EnsureAll(GrantSets.ReadSomething, out var exp))
                throw exp;
            //2018-09-15 2dm replaced
            //var context = GetContext(SxcInstance, Log);
            //PerformSecurityCheck(appIdentity, contentType, Grants.Read, appPath == null ? context.Dnn.Module : null);
            return new EntityApi(appIdentity.AppId, Log).GetEntities(contentType, cultureCode);
        }

        #endregion


        #region GetOne by ID / GUID

	    [HttpGet]
	    [AllowAnonymous] // will check security internally, so assume no requirements
	    public Dictionary<string, object> GetOne(string contentType, int id, string appPath = null)
	        => GetAndSerializeOneAfterSecurityChecks(contentType,
	            appId => new EntityApi(appId, Log).GetOrThrow(contentType, id), appPath);


        [HttpGet]
        [AllowAnonymous]   // will check security internally, so assume no requirements
        public Dictionary<string, object> GetOne(string contentType, Guid guid, string appPath = null)
            => GetAndSerializeOneAfterSecurityChecks(contentType,
                appId => new EntityApi(appId, Log).GetOrThrow(contentType, guid), appPath);
        


        /// <summary>
        /// Preprocess security / context, then get the item based on an passed in method, 
        /// ...then process/finish
        /// </summary>
        /// <param name="contentType"></param>
        /// <param name="getOne"></param>
        /// <param name="appPath"></param>
        /// <returns></returns>
        private Dictionary<string, object> GetAndSerializeOneAfterSecurityChecks(string contentType, Func<int, IEntity> getOne, string appPath)
        {
            Log.Add($"get and serialie after security check type:{contentType}, path:{appPath}");
            // if app-path specified, use that app, otherwise use from context
            var appIdentity = AppFinder.GetAppIdFromPathOrContext(appPath, SxcInstance);

            var itm = getOne(appIdentity.AppId);
            var permCheck = new MultiPermissionsItems(SxcInstance, appIdentity.AppId, itm, Log);
            if (!permCheck.SameAppOrIsSuperUserAndEnsure(GrantSets.ReadSomething, out var exp))
                throw exp;
            //2018-09-15 2dm moved/disabled
            //var context = GetContext(SxcInstance, Log);
            //PerformSecurityCheck(appIdentity, contentType, Grants.Read, appPath == null ? context.Dnn.Module : null, itm);
            return InitEavAndSerializer(appIdentity.AppId).Serializer.Prepare(itm);
        }

        #endregion

        #region ContentBlock - retrieving data of the current instance as is
        [HttpGet]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
	    public HttpResponseMessage GetContentBlockData()
        {
            Log.Add("get content block data");
            // Important note: we are NOT supporting url-view switch at the moment for this
            // reason is, that this kind of data-access is fairly special
            // and not recommended for future use cases, where we have the query etc.
            // IF you want to support View-switching in this, do a deep review w/2dm first!
            // - note that it's really not needed, as you can always use a query or something similar instead
            // - not also that if ever you do support view switching, you will need to ensure security checks

            var dataHandler = new GetContentBlockDataLight(SxcInstance);

            // must access engine to ensure pre-processing of data has happened, 
            // especially if the cshtml contains a override void CustomizeData()
            SxcInstance.GetRenderingEngine(InstancePurposes.PublishData);  

            var dataSource = SxcInstance.Data;
            string json;
            if (dataSource.Publish.Enabled)
            {
                var publishedStreams = dataSource.Publish.Streams;
                var streamList = publishedStreams.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                json = dataHandler.GetJsonFromStreams(dataSource, streamList);
            }
            else
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.Forbidden)
                    {ReasonPhrase = dataHandler.GeneratePleaseEnableDataError(SxcInstance.EnvInstance.Id)});
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return response;
        }
        #endregion



        #region Create
        [HttpPost]
        [AllowAnonymous]   // will check security internally, so assume no requirements
        public Dictionary<string, object> CreateOrUpdate([FromUri] string contentType, [FromBody] Dictionary<string, object> newContentItem, [FromUri] int? id = null, [FromUri] string appPath = null)
        {
            Log.Add($"create or update type:{contentType}, id:{id}, path:{appPath}");
            // if app-path specified, use that app, otherwise use from context
            var appIdentity = AppFinder.GetAppIdFromPathOrContext(appPath, SxcInstance);

            // Check that this ID is actually of this content-type,
            // this throws an error if it's not the correct type
            var itm = id == null
                ? null
                : new EntityApi(appIdentity.AppId, Log).GetOrThrow(contentType, id.Value);

            var ok = itm == null
                ? new MultiPermissionsTypes(SxcInstance, appIdentity.AppId, contentType, Log)
                    .EnsureAll(Grants.Create.AsSet(), out var exp)
                : new MultiPermissionsItems(SxcInstance, appIdentity.AppId, itm, Log)
                    .EnsureAll(Grants.Update.AsSet(), out exp);
            if (!ok)
                throw exp;

            //2018-09-15 2dm moved/disabled
            //var context = GetContext(SxcInstance, Log);
            //PerformSecurityCheck(appIdentity, contentType, perm, appPath == null ? context.Dnn.Module : null, itm);

            // Convert to case-insensitive dictionary just to be safe!
            newContentItem = new Dictionary<string, object>(newContentItem, StringComparer.OrdinalIgnoreCase);

            // Now create the cleaned up import-dictionary so we can create a new entity
            var cleanedNewItem = new AppContentEntityBuilder(Log)
                .CreateEntityDictionary(contentType, newContentItem, appIdentity.AppId);

            var userName = new DnnUser().IdentityToken;

            // try to create
            var currentApp = new App(new DnnTenant(PortalSettings), appIdentity.AppId);
            var publish = Factory.Resolve<IEnvironmentFactory>().PagePublisher(Log);
            currentApp.InitData(false, 
                publish.IsEnabled(ActiveModule.ModuleID), 
                SxcInstance.Data.ConfigurationProvider);
            if (id == null)
            {
                currentApp.Data.Create(contentType, cleanedNewItem, userName);
                // Todo: try to return the newly created object 
                return null;
            }

            currentApp.Data.Update(id.Value, cleanedNewItem, userName);
            return InitEavAndSerializer(appIdentity.AppId).Serializer.Prepare(currentApp.Data.List.One(id.Value));
        }

        #endregion



        #region Delete

        [HttpDelete]
        [AllowAnonymous]   // will check security internally, so assume no requirements
        public void Delete(string contentType, int id, [FromUri] string appPath = null)
        {
            Log.Add($"delete id:{id}, type:{contentType}, path:{appPath}");
            // if app-path specified, use that app, otherwise use from context
            var appIdentity = AppFinder.GetAppIdFromPathOrContext(appPath, SxcInstance);

            // don't allow type "any" on this
            if (contentType == "any")
                throw new Exception("type any not allowed with id-only, requires guid");

            var itm = new EntityApi(appIdentity.AppId, Log).GetOrThrow(contentType, id);
            var permCheck = new MultiPermissionsItems(SxcInstance, appIdentity.AppId, itm, Log);
            if (!permCheck.SameAppOrIsSuperUserAndEnsure(Grants.Delete.AsSet(), out var exp))
                throw exp;
            //2018-09-15 2dm moved/disabled
            //var context = GetContext(SxcInstance, Log);
            //PerformSecurityCheck(appIdentity, itm.Type.Name, Grants.Delete, appPath == null ? context.Dnn.Module : null, itm);
            new EntityApi(appIdentity.AppId, Log).Delete(itm.Type.Name, id);
        }


	    [HttpDelete]
	    [AllowAnonymous]   // will check security internally, so assume no requirements
        public void Delete(string contentType, Guid guid, [FromUri] string appPath = null)
	    {
            Log.Add($"delete guid:{guid}, type:{contentType}, path:{appPath}");
            // if app-path specified, use that app, otherwise use from context
            var appIdentity = AppFinder.GetAppIdFromPathOrContext(appPath, SxcInstance);

            var entityApi = new EntityApi(appIdentity.AppId, Log);
	        var itm = entityApi.GetOrThrow(contentType == "any" ? null : contentType, guid);

	        var permCheck = new MultiPermissionsItems(SxcInstance, appIdentity.AppId, itm, Log);
	        if (!permCheck.SameAppOrIsSuperUserAndEnsure(Grants.Delete.AsSet(), out var exp))
	            throw exp;
	        //2018-09-15 2dm moved/disabled
            //var context = GetContext(SxcInstance, Log);
            //PerformSecurityCheck(appIdentity, itm.Type.Name, Grants.Delete, appPath == null ? context.Dnn.Module : null, itm);

            entityApi.Delete(itm.Type.Name, guid);
        }

        #endregion

        #region GetAssigned - unclear if in use!
        /// <summary>
        /// Get Entities with specified AssignmentObjectTypeId and Key
        /// todo: unclear if this is in use anywhere? 
        /// </summary>
        [HttpGet]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Admin)]
		public IEnumerable<Dictionary<string, object>> GetAssignedEntities(int assignmentObjectTypeId, Guid keyGuid, string contentType, [FromUri] string appPath = null)
        {
            Log.Add($"get assigned for assigmentType#{assignmentObjectTypeId}, guid:{keyGuid}, type:{contentType}, path:{appPath}");
	        return new MetadataController().GetAssignedEntities(assignmentObjectTypeId, "guid", keyGuid.ToString(), contentType);
		}
        #endregion


        #region helpers / initializers to prep the EAV and Serializer

        // 2018-04-18 2dm disabled init-serializer, don't think it's actually ever used!
        private EntitiesController InitEavAndSerializer(int appId)
        {
            Log.Add($"init eav for a#{appId}");
            // Improve the serializer so it's aware of the 2sxc-context (module, portal etc.)
            var entitiesController = new EntitiesController(appId);

            // only do this if we have a real context - otherwise don't do this
            //if (!appId.HasValue)
                ((Serializer)entitiesController.Serializer).Sxc = SxcInstance;
            return entitiesController;
        }
        #endregion

    }
}
