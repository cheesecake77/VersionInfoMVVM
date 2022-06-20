using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Collections.ObjectModel;

namespace VersionInfoMVVM.Models
{
    [Serializable]
    [XmlInclude(typeof(DirectoryDescription))]
    [XmlInclude(typeof(FileDescription))]
    public class DataUnit
    {
        public ObservableCollection<string>? directoryData { get; set; }
        public ObservableCollection<BaseDescription>? fileData { get; set; }
    }
}
