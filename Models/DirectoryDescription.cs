using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace VersionInfoMVVM.Models
{
    [XmlInclude(typeof(BaseDescription))]
    [Serializable]
    public class DirectoryDescription : BaseDescription
    {
        override public string Name => Path;

        public DirectoryDescription()
        {
            IsDirectory = true;
        }
        public override string ToString()
        {
            if (Path is not null)
            return Path;
            return Name;
        }
    }
}
