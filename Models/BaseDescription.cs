using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VersionInfoMVVM.Models
{
    [Serializable]
    public class BaseDescription
    {
        public string Path { get; set; }
        public bool IsDirectory { get; set; }

        virtual public string Name => System.IO.Path.GetFileName(Path);
    }
}
