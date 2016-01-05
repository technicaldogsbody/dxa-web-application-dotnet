using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Models;
using Sdl.Web.Tridion.Linking;
using Sdl.Web.Tridion.Mapping;

namespace Sdl.Web.Tridion.Tests
{
    [TestClass]
    public class DefaultProviderTest
    {
        [TestInitialize]
        public void Initialize()
        {
            TestFixture.InitializeProviders();
            TestRegistration.RegisterCoreViewModels();
        }

        [TestMethod]
        public void GetEntityModel_IsQueryBased_Success()
        {
            string testEntityId = TestFixture.DcpEntityId;

            EntityModel entityModel = SiteConfiguration.ContentProvider.GetEntityModel(testEntityId, TestFixture.ParentLocalization);

            Assert.IsNotNull(entityModel, "entityModel");
            Assert.AreEqual(entityModel.Id, testEntityId, "entityModel.Id");
            Assert.IsNotNull(entityModel.XpmMetadata, "entityModel.XpmMetadata");
            string isQueryBased;
            Assert.IsTrue(entityModel.XpmMetadata.TryGetValue("IsQueryBased", out isQueryBased), "XpmMetadata contains 'IsQueryBased'");
            Assert.AreEqual(isQueryBased, "true", "IsQueryBased value");
        }

        [TestMethod]
        [ExpectedException(typeof(DxaItemNotFoundException))]
        public void GetEntityModel_NonExistent_Exception()
        {
            const string testEntityId = "666-666"; // Should not actually exist
            SiteConfiguration.ContentProvider.GetEntityModel(testEntityId, TestFixture.ParentLocalization);
        }
    }
}
