using System;
using System.Collections.Generic;

namespace September.Editor.PackageImporter
{
    [Serializable]
    public class DriveFileEntry
    {
        public string id;
        public string name;
        public string modifiedTime;
        public string size;
    }

    [Serializable]
    public class DriveFileListResponse
    {
        public DriveFileEntry[] files;
        public string nextPackageToken;
    }

    [Serializable]
    {
        
    }
}