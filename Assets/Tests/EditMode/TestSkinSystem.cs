using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestSkinSystem
    {
        [Test]
        public void SkinData_HasCorrectFields()
        {
            var skin = ScriptableObject.CreateInstance<SkinData>();
            skin.skinName = "Test_plot";
            skin.buildingType = CampBuildingType.Plot;
            skin.hexFillColor = Color.red;
            skin.hexBorderColor = Color.blue;
            skin.costItemName = "Test_pigment";
            skin.costQuantity = 1;

            Assert.AreEqual("Test_plot", skin.skinName);
            Assert.AreEqual(CampBuildingType.Plot, skin.buildingType);
            Assert.AreEqual(Color.red, skin.hexFillColor);
            Assert.AreEqual(Color.blue, skin.hexBorderColor);
            Assert.AreEqual("Test_pigment", skin.costItemName);
            Assert.AreEqual(1, skin.costQuantity);

            Object.DestroyImmediate(skin);
        }

        [Test]
        public void SkinData_DefaultCostQuantity_IsOne()
        {
            var skin = ScriptableObject.CreateInstance<SkinData>();
            Assert.AreEqual(1, skin.costQuantity);
            Object.DestroyImmediate(skin);
        }

        [Test]
        public void PlotSave_HasSkinName()
        {
            var plot = new PlotSave();
            Assert.IsNull(plot.skinName);
            plot.skinName = "Basil_plot";
            Assert.AreEqual("Basil_plot", plot.skinName);
        }

        [Test]
        public void VaseSave_HasSkinName()
        {
            var vase = new VaseSave();
            Assert.IsNull(vase.skinName);
            vase.skinName = "Basil_vase";
            Assert.AreEqual("Basil_vase", vase.skinName);
        }

        [Test]
        public void MallumHouseSave_HasSkinName()
        {
            var house = new MallumHouseSave();
            Assert.IsNull(house.skinName);
            house.skinName = "Basil_house";
            Assert.AreEqual("Basil_house", house.skinName);
        }

        [Test]
        public void SkinName_SurvivesJsonRoundTrip()
        {
            var plot = new PlotSave { skinName = "Lavender_plot" };
            string json = JsonUtility.ToJson(plot);
            var deserialized = JsonUtility.FromJson<PlotSave>(json);
            Assert.AreEqual("Lavender_plot", deserialized.skinName);
        }
    }
}
