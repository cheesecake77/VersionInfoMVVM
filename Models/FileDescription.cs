using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace VersionInfoMVVM.Models
{
    [Serializable]
    [XmlInclude(typeof(BaseDescription))]
    public class FileDescription : BaseDescription, IEquatable<FileDescription>
    {
        public FileDescription FillProperties()
        {
            if (Path is string path)
            try
            {
                var fileInfo = new FileInfo(Path);
                this.Size = fileInfo.Length;
                this.Time = fileInfo.LastWriteTime;
                var fileVersion = FileVersionInfo.GetVersionInfo(Path);
                this.Version = fileVersion?.FileVersion ?? "";
                this.Hash = ComputeHash(Path);
                this.FileState = FileState.Ok;
            }
            catch (Exception)
            {
                throw new Exception("Ошибка при получении данных о файлах");
            }
            return this;
        }
        public FileDescription()
        {
            IsDirectory = false;
        }

        public long Size { get; set; }
        public DateTime Time { get; set; }

        public string? Version { get; set; }
        public string? Hash { get; set; }

        public FileState FileState { get; set; }

        public override string ToString()
        {
            return $"{Name} {Version} {Time}{Size} байт {Hash}";
        }
        private static string ComputeHash(string path)
        {
            try
            {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(path);
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception error)
            {
                Trace.WriteLine(error.Message);
            }
            return "--";
        }

        public bool Equals(FileDescription? other)
        {
            if (other == null) return false;
            if (this.Size != other.Size) return false;
            if (this.Time.ToString("G") != other.Time.ToString("G")) return false;
            if (this.Version != other.Version) return false;
            if (this.Hash != other.Hash) return false;
            return true;
        }
    }

    public enum FileState
    {
        Ok,
        Added,
        Modified,
        Deleted
    }
}
