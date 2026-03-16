using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class LocalizationServiceTests
    {
        private LocalizationService service;
        private GameObject go;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("TestLocalization");
            service = go.AddComponent<LocalizationService>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        [Test]
        public void Get_ReturnsValue_WhenKeyExists()
        {
            var data = new Dictionary<string, string>
            {
                { "ui.button.harvest", "Harvest" },
                { "ui.label.empty", "Empty" }
            };
            service.LoadTranslations(data, "en");

            Assert.AreEqual("Harvest", service.Get("ui.button.harvest", "fallback"));
            Assert.AreEqual("Empty", service.Get("ui.label.empty", "fallback"));
        }

        [Test]
        public void Get_ReturnsFallback_WhenKeyMissing()
        {
            var data = new Dictionary<string, string>
            {
                { "ui.button.harvest", "Harvest" }
            };
            service.LoadTranslations(data, "en");

            Assert.AreEqual("fallback text", service.Get("ui.nonexistent.key", "fallback text"));
        }

        [Test]
        public void Get_ReturnsFallback_WhenStringsEmpty()
        {
            service.LoadTranslations(new Dictionary<string, string>(), "en");

            Assert.AreEqual("default", service.Get("any.key", "default"));
        }

        [Test]
        public void LoadTranslations_ReplacesPreviousDictionary()
        {
            var data1 = new Dictionary<string, string> { { "key1", "value1" } };
            service.LoadTranslations(data1, "en");
            Assert.AreEqual("value1", service.Get("key1", "fallback"));

            var data2 = new Dictionary<string, string> { { "key2", "value2" } };
            service.LoadTranslations(data2, "ja");
            Assert.AreEqual("fallback", service.Get("key1", "fallback"));
            Assert.AreEqual("value2", service.Get("key2", "fallback"));
            Assert.AreEqual("ja", service.CurrentLocale);
        }

        [Test]
        public void DetectDeviceLocale_ReturnsNonEmptyString()
        {
            // DetectDeviceLocale is static and depends on Application.systemLanguage
            // In editor, this will return whatever the editor language is
            // We can at least verify it returns a non-null, non-empty string
            var locale = LocalizationService.DetectDeviceLocale();
            Assert.IsNotNull(locale);
            Assert.IsNotEmpty(locale);
        }

        [Test]
        public void SetSupportedLocales_UpdatesList()
        {
            var locales = new List<string> { "en", "ja", "de" };
            service.SetSupportedLocales(locales);
            Assert.AreEqual(3, service.SupportedLocales.Count);
            Assert.Contains("ja", service.SupportedLocales);
        }

        [Test]
        public void SetSupportedLocales_DefaultsToEn_WhenNull()
        {
            service.SetSupportedLocales(null);
            Assert.AreEqual(1, service.SupportedLocales.Count);
            Assert.AreEqual("en", service.SupportedLocales[0]);
        }

        [Test]
        public void Loc_Get_ReturnsFallback_WhenNoInstance()
        {
            // Destroy the instance so Loc.Get falls back
            Object.DestroyImmediate(go);
            go = null;
            // DestroyImmediate fires OnDestroy synchronously, which sets Instance = null

            Assert.AreEqual("fallback", Loc.Get("any.key", "fallback"));
        }
    }
}
