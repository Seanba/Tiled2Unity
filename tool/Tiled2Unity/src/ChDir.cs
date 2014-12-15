using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public class ChDir : IDisposable
    {
        private string directoryOld;
        private string directoryNow;

        public ChDir(string path)
        {
            this.directoryOld = Directory.GetCurrentDirectory();
            if (Directory.Exists(path))
                this.directoryNow = path;
            else if (File.Exists(path))
                this.directoryNow = Path.GetDirectoryName(path);
            else
                throw new DirectoryNotFoundException(String.Format("Cannot set current directory. Does not exist: {0}", path));

            Directory.SetCurrentDirectory(this.directoryNow);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(this.directoryOld);
        }
    }
}
