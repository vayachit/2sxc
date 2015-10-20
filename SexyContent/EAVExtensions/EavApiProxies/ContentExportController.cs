﻿using System.Collections.Generic;
using System.Net.Http;
using System.Web.Http;
using DotNetNuke.Entities.Content;
using DotNetNuke.Security;
using DotNetNuke.Web.Api;
using ToSic.Eav;
using ToSic.Eav.ImportExport.Refactoring.Options;
using ToSic.SexyContent.WebApi;
using static ToSic.Eav.WebApi.ContentImportController;

namespace ToSic.SexyContent.EAVExtensions.EavApiProxies
{
    /// <summary>
    /// Web API Controller for the Pipeline Designer UI
    /// </summary>
    // [SupportedModules("2sxc,2sxc-app")]
    
    // [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Anonymous)]
    [AllowAnonymous]
    public class ContentExportController : DnnApiController // SxcApiController
	{
        private readonly Eav.WebApi.ContentExportController eavCtc;
        public ContentExportController()
        {
            eavCtc = new Eav.WebApi.ContentExportController();
        }


        [HttpGet]
        [AllowAnonymous]
        public HttpResponseMessage ExportContent(int appId, string language, string defaultLanguage, string contentType,
            RecordExport recordExport, ResourceReferenceExport resourcesReferences,
            LanguageReferenceExport languageReferences)
        {
            // do security check
            if(!PortalSettings.UserInfo.IsInRole("Administrators")) // note: user.isinrole didn't work
                throw new HttpRequestException("Needs admin permissions to do this");
            return eavCtc.ExportContent(appId, language, defaultLanguage, contentType, recordExport, resourcesReferences,
                languageReferences);
        }


    }
}