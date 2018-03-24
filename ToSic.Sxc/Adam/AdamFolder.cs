﻿using System.Collections.Generic;
using ToSic.Eav.Apps.Assets;
using ToSic.SexyContent;

// ReSharper disable once CheckNamespace
namespace ToSic.Sxc.Adam
{
    public class AdamFolder : Folder, IAdamItem
    {

        public AdamBrowseContext AdamBrowseContext;
        public AdamManager Manager;

        private readonly IEnvironmentFileSystem _fs;

        public AdamFolder(IEnvironmentFileSystem envFs)
        {
            _fs = envFs;
        }
        /// <summary>
        /// Metadata for this folder
        /// This is usually an entity which has additional information related to this file
        /// </summary>
        public DynamicEntity Metadata => AdamBrowseContext.GetFirstMetadata(Id, true);

        public bool HasMetadata => AdamBrowseContext.GetFirstMetadataEntity(Id, false) != null;

        public string Url => AdamBrowseContext.GenerateWebPath(this);

        public string Type => Classification.Folder;

        private IEnumerable<AdamFolder> _folders;

        /// <summary>
        ///  Get all subfolders
        /// </summary>
        public IEnumerable<AdamFolder> Folders
        {
            get
            {
                if (_folders != null) return _folders;

                // this is to skip it if it doesn't have subfolders...
                if (!HasChildren || string.IsNullOrEmpty(Name))
                    return _folders = new List<AdamFolder>();
                
                _folders = _fs.GetFolders(Id, AdamBrowseContext);
                return _folders;
            }
        }


        private IEnumerable<AdamFile> _files;


        /// <summary>
        /// Get all files in this folder
        /// </summary>
        public IEnumerable<AdamFile> Files 
            => _files ?? (_files = _fs.GetFiles(Id, AdamBrowseContext));
    }
}