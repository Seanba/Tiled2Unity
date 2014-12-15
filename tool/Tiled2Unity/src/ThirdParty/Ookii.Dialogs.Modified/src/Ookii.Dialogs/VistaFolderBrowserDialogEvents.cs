using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// [seanba] - Added VistaFolderBrowserDialogEvents so we can get events when selection changes, etc.. 
namespace Ookii.Dialogs
{
    class VistaFolderBrowserDialogEvents : Ookii.Dialogs.Interop.IFileDialogEvents
    {
        private VistaFolderBrowserDialog _dialog;

        public VistaFolderBrowserDialogEvents(VistaFolderBrowserDialog dialog)
        {
            if( dialog == null )
                throw new ArgumentNullException("dialog");

            _dialog = dialog;
        }

        public Interop.HRESULT OnFileOk(Interop.IFileDialog pfd)
        {
            // [seanba] - We could put extra validation here to stop dialog from closing.
            return Interop.HRESULT.S_OK;
        }

        public Interop.HRESULT OnFolderChanging(Interop.IFileDialog pfd, Interop.IShellItem psiFolder)
        {
            //CheckSelectedFolder(psiFolder);
            return Interop.HRESULT.S_OK;
        }

        public void OnFolderChange(Interop.IFileDialog pfd)
        {
            //CheckSelectedFolder(pfd);
        }

        public void OnSelectionChange(Interop.IFileDialog pfd)
        {
            CheckSelectedFolder(pfd);
        }

        private void CheckSelectedFolder(Interop.IFileDialog pfd)
        {
            Interop.IShellItem psiFolder;
            pfd.GetCurrentSelection(out psiFolder);
            CheckSelectedFolder(pfd, psiFolder);
        }

        private void CheckSelectedFolder(Interop.IFileDialog pfd, Interop.IShellItem psiFolder)
        {
            // 
            NativeMethods.SFGAOF fileSystemFlags = 0;
            psiFolder.GetAttributes(NativeMethods.SFGAOF.SFGAO_FILESYSTEM, out fileSystemFlags);
            if (fileSystemFlags != 0)
            {
                // A filesystem folder, but is it valid?
                string path;
                psiFolder.GetDisplayName(NativeMethods.SIGDN.SIGDN_FILESYSPATH, out path);

                if (IsShortcut(path))
                {
                    path = ResolveShortcut(path);
                }

                //Console.WriteLine("filesyspath path: {0}", path);
                _dialog.DoFolderSelected(path);
            }
            else
            {
                // Not a filesystem, let the user know
                _dialog.DoFolderSelected("");
            }
        }

        private static bool IsShortcut(string path)
        {
            string directory = Path.GetDirectoryName(path);
            string file = Path.GetFileName(path);

            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder folder = shell.NameSpace(directory);
            Shell32.FolderItem folderItem = folder.ParseName(file);

            if (folderItem != null)
                return folderItem.IsLink;
            return false;
        }

        private static string ResolveShortcut(string path)
        {
            string directory = Path.GetDirectoryName(path);
            string file = Path.GetFileName(path);

            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder folder = shell.NameSpace(directory);
            Shell32.FolderItem folderItem = folder.ParseName(file);

            Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;
            return link.Path;
        }

        public void OnShareViolation(Interop.IFileDialog pfd, Interop.IShellItem psi, out NativeMethods.FDE_SHAREVIOLATION_RESPONSE pResponse)
        {
            pResponse = NativeMethods.FDE_SHAREVIOLATION_RESPONSE.FDESVR_DEFAULT;
        }

        public void OnTypeChange(Interop.IFileDialog pfd)
        {
        }

        public void OnOverwrite(Interop.IFileDialog pfd, Interop.IShellItem psi, out NativeMethods.FDE_OVERWRITE_RESPONSE pResponse)
        {
            pResponse = NativeMethods.FDE_OVERWRITE_RESPONSE.FDEOR_DEFAULT;
        }
    }
}
