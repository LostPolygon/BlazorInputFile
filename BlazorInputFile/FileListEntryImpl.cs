using System;
using System.IO;

namespace BlazorInputFile
{
    // This is public only because it's used in a JSInterop method signature,
    // but otherwise is intended as internal
    public class FileListEntryImpl : IFileListEntry, IEquatable<FileListEntryImpl>
    {
        internal InputFile Owner { get; set; }

        private Stream _stream;

        public event EventHandler OnDataRead;

        public int Id { get; set; }

        public DateTime LastModified { get; set; }

        public string Name { get; set; }

        public long Size { get; set; }

        public string Type { get; set; }

        public Stream Data
        {
            get
            {
                _stream ??= Owner.OpenFileStream(this);
                return _stream;
            }
        }

        internal void RaiseOnDataRead()
        {
            OnDataRead?.Invoke(this, null);
        }

        public void Dispose() {
            _stream?.Dispose();
            _stream = null;
        }

        public bool Equals(FileListEntryImpl other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Owner.InputFileElement.Id, other.Owner.InputFileElement.Id) && Id == other.Id;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FileListEntryImpl) obj);
        }

        public override int GetHashCode() {
            return HashCode.Combine(Owner.InputFileElement.Id, Id);
        }

        public static bool operator ==(FileListEntryImpl left, FileListEntryImpl right) {
            return Equals(left, right);
        }

        public static bool operator !=(FileListEntryImpl left, FileListEntryImpl right) {
            return !Equals(left, right);
        }
    }
}
