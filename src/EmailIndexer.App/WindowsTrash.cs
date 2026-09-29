using System;
using EmailIndexer.Core.Index;
using Microsoft.VisualBasic.FileIO;

namespace EmailIndexer.App
{
    /// <summary>Windows 휴지통으로 보내기 (관리자 권한 불필요, 복원 가능).</summary>
    internal sealed class WindowsTrash : ITrash
    {
        public bool SendToRecycleBin(string path, out string? error)
        {
            try
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
