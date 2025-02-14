﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    public abstract class StorageTest
    {
        protected const int DataLength = 2048;
        protected readonly byte[] DummyData = Helper.DummyData.GenerateRandomBytes(DataLength);
        protected IStorage Storage { get; set; }

        [TestInitialize]
        public abstract void Initial();

        [TestMethod]
        public void OpenReadLengthTest()
        {
            // arrange
            Storage.WriteAsync(DummyData, 0, DataLength).Wait();

            // act
            var reader = Storage.OpenRead();

            // assert
            Assert.AreEqual(DataLength, reader.Length);
        }

        [TestMethod]
        public void OpenReadStreamTest()
        {
            // arrange
            Storage.WriteAsync(DummyData, 0, DataLength).Wait();

            // act
            var reader = Storage.OpenRead();

            // assert
            for (int i = 0; i < DataLength; i++)
            {
                Assert.AreEqual(DummyData[i], reader.ReadByte());
            }
        }

        [TestMethod]
        public void WriteAsyncLengthTest()
        {
            // arrange
            var length = DataLength / 2;

            // act
            Storage.WriteAsync(DummyData, 0, length).Wait();

            // assert
            Assert.AreEqual(length, Storage.GetLength());
        }

        [TestMethod]
        public void WriteAsyncBytesTest()
        {
            // arrange
            var length = DataLength / 2;

            // act
            Storage.WriteAsync(DummyData, 0, length).Wait();
            var reader = Storage.OpenRead();

            // assert
            for (int i = 0; i < length; i++)
            {
                Assert.AreEqual(DummyData[i], reader.ReadByte());
            }
        }

        [TestMethod]
        public void WriteAsyncMultipleTimeTest()
        {
            // arrange
            var count = 128;
            var writeCount = DataLength / count;

            // act
            for (int i = 0; i < count; i++)
            {
                Storage.WriteAsync(DummyData, writeCount * i, writeCount).Wait();
            }

            // assert
            var reader = Storage.OpenRead();
            for (int i = 0; i < DataLength; i++)
            {
                Assert.AreEqual(DummyData[i], reader.ReadByte());
            }
        }

        [TestMethod]
        public void WriteAsyncOutOfRangeExceptionTest()
        {
            // arrange
            var offset = 1;

            // act
            Task WriteMethod() => Storage.WriteAsync(DummyData, offset, DataLength);

            // assert
            Assert.ThrowsExceptionAsync<ArgumentException>(WriteMethod);
        }

        [TestMethod]
        public void ClearTest()
        {
            // arrange
            Storage.WriteAsync(DummyData, 0, DataLength).Wait();

            // act
            Storage.Clear();

            // assert
            Assert.AreEqual(0, Storage.GetLength());
        }

        [TestMethod]
        public void FlushTest()
        {
            // arrange
            Storage.WriteAsync(DummyData, 0, DataLength).Wait();

            // act
            Storage.Flush();

            // assert
            Assert.AreEqual(DummyData.Length, Storage.GetLength());
        }

        [TestMethod]
        public void GetLengthTest()
        {
            // arrange
            var data = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 };
            Storage.WriteAsync(data, 0, 1).Wait();

            // act
            var actualLength = Storage.GetLength();

            // assert
            Assert.AreEqual(1, actualLength);
        }

        [TestMethod]
        public void TestStreamExpandability()
        {
            // arrange
            var data = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 };
            Storage.WriteAsync(data, 0, data.Length).Wait();
            Storage.Flush();

            // act
            var serializedStream = JsonConvert.SerializeObject(Storage);
            var mutableStream = JsonConvert.DeserializeObject(serializedStream, Storage.GetType()) as IStorage;
            mutableStream?.WriteAsync(data, 0, data.Length);

            // assert
            Assert.AreEqual(data.Length*2, mutableStream?.GetLength());

            Storage.Clear();
        }
    }
}