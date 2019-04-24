
using System;
using System.IO;
using System.Text;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Collections;
using System.Diagnostics;
using System.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Permissions;

using Box.V2;
using Box.V2.Config;
using Box.V2.JWTAuth;
using Box.V2.Models;


namespace Utilities
{
    public  class FileSystem : IFileSystem
    {
        
        private BoxClient boxClient = null;

        public FileSystem()
        {
            CreateBoxClient();
        }

        //ToDo: change to accept the token or userId as parameter based on the windows authenticated user making the call
        private void CreateBoxClient()
        {
            IBoxConfig config = null;
            using (FileStream fs = new FileStream("CONFIG.JSON", FileMode.Open))
            {
                config = BoxConfig.CreateFromJsonFile(fs);
            }

            var userId = "<BoxUuserID>";
            var session = new BoxJWTAuth(config);
            var userToken = session.UserToken(userId);
            boxClient = session.UserClient(userToken, userId);
        } 
        private object GetFileOrFolderInfo(string fileId, string Type)
        {

            try
            {
                if (Type == "file")
                {
                    var fileInfo = boxClient.FilesManager.GetInformationAsync(fileId);
                    fileInfo.Wait();
                    return fileInfo.Result;
                }

                if (Type == "folder")
                {
                    var folderInfo = boxClient.FoldersManager.GetInformationAsync(fileId);
                    folderInfo.Wait();
                    return folderInfo.Result;
                }

            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message, EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);

            }

            return null;
        }

        private string GetFileOrFolderIDfromPath(string path, ref string Type)
        {
            string fileId = null;
            Type = "folder";
            var folderNames = path.Remove(0, 2).Split('/');
            folderNames = folderNames.Where((f) => !String.IsNullOrEmpty(f)).ToArray(); //get rid of leading empty entry in case of leading slash
            var folderNameList = folderNames.ToList();
            //folderNameList.RemoveAt(folderNameList.Count - 1);

            folderNames = folderNameList.ToArray();

            //Task<BoxFolder> folderInfo=null;
            //BoxFolder x = null;
            var currFolderId = "0"; //the root folder is always "0"
            foreach (var folderName in folderNames)
            {
                try
                {

                    var folderInfo = boxClient.FoldersManager.GetFolderItemsAsync(currFolderId, 100);

                    folderInfo.Wait();
                    BoxCollection<BoxItem> collAll = new BoxCollection<BoxItem>();
                    collAll.Entries = new List<BoxItem>();
                    collAll.Entries.AddRange(folderInfo.Result.Entries);

                    if (folderInfo.Result.TotalCount > 100)
                    {
                        for (int i = 1; i <= folderInfo.Result.TotalCount / 100 + 1; i++)
                        {

                            var folderItems = boxClient.FoldersManager.GetFolderItemsAsync(currFolderId, i * 100);
                            folderItems.Wait();
                            if (folderItems.Result.Entries.Count > 0)
                                collAll.Entries.AddRange(folderItems.Result.Entries);
                        }

                    }

                    var foundFolder =
                        collAll.Entries.OfType<BoxFolder>().First((f) => f.Name == folderName);
                    currFolderId = foundFolder.Id;
                }
                catch (Exception e)
                {
                    try
                    {
                        //get fileid instead of folderid
                        Type = "file";
                    }
                    catch (Exception ex)
                    {
                        currFolderId = null;
                    }

                }

            }

            fileId = currFolderId;

            return fileId;
        }

        

        public void Delete(string path)
        {
            try
            {
                string itemType=null;
                string fileId = GetFileOrFolderIDfromPath(path, ref itemType);

                if (itemType == "file")
                {
                    var deleteFile = boxClient.FilesManager.DeleteAsync(fileId);
                    deleteFile.Wait();
                }

                if (itemType == "folder")
                {
                    var deleteFolder = boxClient.FoldersManager.DeleteAsync(fileId);
                    deleteFolder.Wait();
                }

            }
            catch(Exception e)
            {
                if(e.InnerException!=null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message,EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);
            }
        }

        public void Move(string source, string destination)
        {
            try
            {
                string itemType = null;
                string sourceId = GetFileOrFolderIDfromPath(source, ref itemType);
                string destinationId = GetFileOrFolderIDfromPath(destination, ref itemType);

                if (itemType == "file")
                {
                    var fileRequest = new BoxFileRequest();
                    fileRequest.Parent = new BoxRequestEntity() { Id = destinationId, Type = BoxType.folder};
                    fileRequest.Id = sourceId;
                    var moveFile = boxClient.FilesManager.UpdateInformationAsync(fileRequest);
                    moveFile.Wait();
                }

                if (itemType == "folder")
                {
                    var folderRequest = new BoxFolderRequest();
                    folderRequest.Parent = new BoxRequestEntity() { Id = destinationId, Type = BoxType.folder };
                    folderRequest.Id = sourceId;
                    var moveFolder = boxClient.FoldersManager.UpdateInformationAsync(folderRequest);
                    moveFolder.Wait();
                }

            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message, EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);
            }
        }



        public FileSystemEntry GetEntry(string path)
        {
            FileSystemEntry fileSystemEntry = null;

            string itemType = null;
            
            try
            {
                string sourceId = GetFileOrFolderIDfromPath(path, ref itemType);
                
                if (itemType == "file")
                {
                    BoxFile file=((BoxFile) GetFileOrFolderInfo(sourceId, itemType));

                    fileSystemEntry = new FileSystemEntry(path,file.Name,false,ulong.Parse( file.Size.ToString()),  DateTime.Parse(file.CreatedAt.ToString()), DateTime.Parse(file.ModifiedAt.ToString()), DateTime.Parse(file.ModifiedAt.ToString()),false,false, false);
                    fileSystemEntry.id = sourceId;
                }

                if (itemType == "folder")
                {
                    BoxFolder folder = ((BoxFolder)GetFileOrFolderInfo(sourceId, itemType));

                    fileSystemEntry = new FileSystemEntry(path, folder.Name,true, ulong.Parse(folder.Size.ToString()), DateTime.Parse(folder.CreatedAt.ToString()), DateTime.Parse(folder.ModifiedAt.ToString()), DateTime.Parse(folder.ModifiedAt.ToString()), false, false, false);
                    fileSystemEntry.id = sourceId;
                }

                
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message, EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);
            }

            return fileSystemEntry;
        }


        public List<FileSystemEntry> ListEntriesInRootDirectory()
        {
            return ListEntriesInDirectory(@"\");
        }

        public virtual List<KeyValuePair<string, ulong>> ListDataStreams(string path)
        {
            FileSystemEntry entry = GetEntry(path);
            List<KeyValuePair<string, ulong>> result = new List<KeyValuePair<string, ulong>>();
            if (!entry.IsDirectory)
            {
                result.Add(new KeyValuePair<string, ulong>("::$DATA", entry.Size));
            }
            return result;
        }

        public Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            return OpenFile(path, mode, access, share, FileOptions.None);
        }

        public Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share, FileOptions options)
        {
            Stream stream = null;
            try
            {
                string itemType = null;
                string sourceId = GetFileOrFolderIDfromPath(path, ref itemType);

                var fileDownload = boxClient.FilesManager.DownloadStreamAsync(sourceId);
                fileDownload.Wait();
                stream = fileDownload.Result;
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message, EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);
            }

            return stream;
        }

        public void CopyFile(string sourcePath, string destinationPath)
        {
            try
            {
                //const int bufferLength = 1024 * 1024;
                FileSystemEntry sourceFile = GetEntry(sourcePath);
                FileSystemEntry destinationFile = GetEntry(destinationPath);
                if (sourceFile == null | sourceFile.IsDirectory)
                {
                    throw new FileNotFoundException();
                }

                if (destinationFile != null && !destinationFile.IsDirectory)
                {
                    throw new ArgumentException("Destination cannot be a directory");
                }

                if (destinationFile == null)
                {
                    throw new ArgumentException("Destination not specified");
                }
                var fileRequest = new BoxFileRequest();
                fileRequest.Id = sourceFile.id;
                fileRequest.Parent = new BoxRequestEntity() { Id = destinationFile.id, Type = BoxType.folder };
                var copyFile = boxClient.FilesManager.CopyAsync(fileRequest);
                copyFile.Wait();
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message, EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);
            }

            //Stream sourceStream = OpenFile(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.SequentialScan);
            //Stream destinationStream = OpenFile(destinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.None);
            //while (sourceStream.Position < sourceStream.Length)
            //{
            //    int readSize = (int)Math.Max(bufferLength, sourceStream.Length - sourceStream.Position);
            //    byte[] buffer = new byte[readSize];
            //    sourceStream.Read(buffer, 0, buffer.Length);
            //    destinationStream.Write(buffer, 0, buffer.Length);
            //}
            //sourceStream.Close();
            //destinationStream.Close();
        }

        public void SetAttributes(string path, bool? isHidden, bool? isReadonly, bool? isArchived)
        {
            try
            {
                //no need to do anything here since those attirbutes don't apply to Box
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message, EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);
            }
        }

        public void SetDates(string path, DateTime? creationDT, DateTime? lastWriteDT, DateTime? lastAccessDT)
        {
            try
            {
                //no need to do anything here since those attirbutes should not be changed manually
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message, EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);
            }
        }
        
        public FileSystemEntry CreateDirectory(string path)
        {
            FileSystemEntry fileSystemEntry = null;
            string itemType = null;
            string parentPath = "";// + RemoveLastFolderFromPath(path);
            string parentFolderId =GetFileOrFolderIDfromPath(parentPath, ref itemType);

            try
            {
                var boxEntityRequest = new BoxRequestEntity() { Id = parentFolderId, Type = BoxType.folder };
                BoxFolderRequest folderRequest = new BoxFolderRequest() { Parent = boxEntityRequest };
                var createDirectory = boxClient.FoldersManager.CreateAsync(folderRequest);
                createDirectory.Wait();
                fileSystemEntry = GetEntry(path);
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message, EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);
            }

            return fileSystemEntry;
        }
        private string RemoveLastFolderFromPath(string path)
        {
            string result = null;
            DirectoryInfo parentDir = Directory.GetParent(path);
            // or possibly
            //DirectoryInfo parentDir = Directory.GetParent(path.EndsWith("\\") ? path : string.Concat(path, "\\"));

            // The result is available here
            result = parentDir.Parent.FullName;
                               
            return result;
        }

        public FileSystemEntry CreateFile(string path)
        {
            FileSystemEntry fileSystemEntry = null;
            try
            {
                // creating an empty file is meaningless in box
                // can be updated later to reflect a file upload but the source file path needs to be passed somehow 
                // to the function without changing its signature
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message, EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);
            }

            return fileSystemEntry;
        }
        public List<FileSystemEntry> ListEntriesInDirectory(string path)
        {
            List<FileSystemEntry> fileSystemEntryList =null;

            try
            {
                string itemType = null;

                string currFolderId = GetFileOrFolderIDfromPath(path, ref itemType);

                var folderInfo = boxClient.FoldersManager.GetFolderItemsAsync(currFolderId, 100);

                folderInfo.Wait();
                BoxCollection<BoxItem> collAll = new BoxCollection<BoxItem>();
                collAll.Entries = new List<BoxItem>();
                collAll.Entries.AddRange(folderInfo.Result.Entries);

                if (folderInfo.Result.TotalCount > 100)
                {
                    for (int i = 1; i <= folderInfo.Result.TotalCount / 100 + 1; i++)
                    {

                        var folderItems = boxClient.FoldersManager.GetFolderItemsAsync(currFolderId, i * 100);
                        folderItems.Wait();
                        if (folderItems.Result.Entries.Count > 0)
                            collAll.Entries.AddRange(folderItems.Result.Entries);
                    }

                }

                fileSystemEntryList = new List<FileSystemEntry>();

                foreach (BoxItem item in collAll.Entries)
                {
                    fileSystemEntryList.Add(GetEntry(path + "\\" + item.Name));
                }

            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    EventLog.WriteEntry("Box SMB", e.Message + " Inner Exception: " + e.InnerException.Message, EventLogEntryType.Error);
                else
                    EventLog.WriteEntry("Box SMB", e.Message, EventLogEntryType.Error);
            }

            return fileSystemEntryList;
        }

        public virtual bool Exists(string path)
        {
            try
            {
                GetEntry(path);
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }

            return true;
        }

        public  string Name
        {
            get;
        }

        public  long Size
        {
            get;
        }

        public  long FreeSpace
        {
            get;
        }

        public  bool SupportsNamedStreams
        {
            get;
        }

        public static string GetParentDirectory(string path)
        {
            if (path == String.Empty)
            {
                path = @"\";
            }

            if (!path.StartsWith(@"\"))
            {
                throw new ArgumentException("Invalid path");
            }

            if (path.Length > 1 && path.EndsWith(@"\"))
            {
                path = path.Substring(0, path.Length - 1);
            }

            int separatorIndex = path.LastIndexOf(@"\");
            return path.Substring(0, separatorIndex + 1);
        }

        /// <summary>
        /// Will append a trailing slash to a directory path if not already present
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetDirectoryPath(string path)
        {
            if (path.EndsWith(@"\"))
            {
                return path;
            }
            else
            {
                return path + @"\";
            }
        }
    }
}
