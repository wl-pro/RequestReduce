﻿using System;
using System.Collections.Generic;
using System.IO;
using RequestReduce.Configuration;
using RequestReduce.Utilities;

namespace RequestReduce.Store
{
    public class LocalDiskStore : IStore
    {
        private readonly IFileWrapper fileWrapper;
        private readonly IRRConfiguration configuration;
        private readonly IUriBuilder uriBuilder;
        private FileSystemWatcher watcher = new FileSystemWatcher();
        protected DeleeCsAction DeleteCssAction;
        protected AddCssAction AddCssAction;

        public LocalDiskStore(IFileWrapper fileWrapper, IRRConfiguration configuration, IUriBuilder uriBuilder)
        {
            this.fileWrapper = fileWrapper;
            this.configuration = configuration;
            configuration.PhysicalPathChange += SetupWatcher;
            this.uriBuilder = uriBuilder;
            SetupWatcher();
        }

        protected LocalDiskStore()
        {
        }

        protected virtual void SetupWatcher()
        {
            if (string.IsNullOrEmpty(configuration.SpritePhysicalPath)) return;
            watcher.Path = configuration.SpritePhysicalPath;
            watcher.IncludeSubdirectories = true;
            watcher.Filter = "*.css";
            watcher.Created += new FileSystemEventHandler(OnChange);
            watcher.Deleted += new FileSystemEventHandler(OnChange);
            watcher.EnableRaisingEvents = true;
        }

        private void OnChange(object sender, FileSystemEventArgs e)
        {
            var path = e.FullPath;
            var dir = path.Substring(0, path.LastIndexOf("\\"));
            var keyDir = dir.Substring(dir.LastIndexOf("\\") + 1);
            Guid guid;
            if(Guid.TryParse(keyDir, out guid))
            {
                if (e.ChangeType == WatcherChangeTypes.Deleted && DeleteCssAction != null)
                    DeleteCssAction(guid);
                if (e.ChangeType == WatcherChangeTypes.Created && AddCssAction != null)
                    AddCssAction(guid, uriBuilder.BuildCssUrl(guid));
            }
        }

        public void Save(byte[] content, string url)
        {
            fileWrapper.Save(content, GetFileNameFromConfig(url));
        }

        public byte[] GetContent(string url)
        {
            throw new NotImplementedException();
        }

        public Stream OpenStream(string url)
        {
            return fileWrapper.OpenStream(GetFileNameFromConfig(url));
        }

        public IDictionary<Guid, string> GetSavedUrls()
        {
            var dic = new Dictionary<Guid, string>();
            if (string.IsNullOrEmpty(configuration.SpritePhysicalPath))
                return dic;
            var keys = fileWrapper.GetDirectories(configuration.SpritePhysicalPath);
            foreach (var key in keys)
            {
                Guid guid;
                var success = Guid.TryParse(key.Substring(key.LastIndexOf('\\') + 1), out guid);
                if(success)
                    dic.Add(guid, uriBuilder.BuildCssUrl(guid));
            }
            return dic;
        }

        public void RegisterDeleteCssAction(DeleeCsAction deleteAction)
        {
            DeleteCssAction = deleteAction;
        }

        public void RegisterAddCssAction(AddCssAction addAction)
        {
            AddCssAction = addAction;
        }

        private string GetFileNameFromConfig(string url)
        {
            var fileName = url;
            if (!string.IsNullOrEmpty(configuration.ContentHost))
                fileName = url.Replace(configuration.ContentHost, "");
            fileName = fileName.Replace(configuration.SpriteVirtualPath, configuration.SpritePhysicalPath).Replace('/', '\\');
            var dir = fileName.Substring(0, fileName.LastIndexOf('\\'));
            if(!fileWrapper.DirectoryExists(dir))
                fileWrapper.CreateDirectory(dir);
            return fileName;
        }
    }
}
