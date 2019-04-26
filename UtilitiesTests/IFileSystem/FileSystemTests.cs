using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utilities.Tests
{
    [TestClass()]
    public class FileSystemTests
    {
        [TestMethod()]
        public void FileSystemTest()
        {
            FileSystem boxFS = new FileSystem();

            if (boxFS == null)
                Assert.Fail();
        }

        [TestMethod()]
        public void GetEntryTest()
        {
            FileSystem boxFS = new FileSystem();

            if (boxFS.GetEntry(@"\\MM\All Purge Errors - raw.xlsx") == null)
                Assert.Fail();
        }

        [TestMethod()]
        public void ListEntriesInDirectoryTest()
        {

            FileSystem boxFS = new FileSystem();

            //if (boxFS.ListEntriesInDirectory(@"\\MM") == null)
            if (boxFS.ListEntriesInDirectory(@"\\") == null)
                Assert.Fail();

        }

        [TestMethod()]
        public void OpenFileTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void CopyFileTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void DeleteTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void MoveTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void CreateDirectoryTest()
        {
            Assert.Fail();
        }
    }
}