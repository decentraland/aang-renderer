using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    [TestFixture]
    [TestOf(typeof(URLParameters))]
    public class URLParametersTest
    {
        [Test]
        public void TestInitialize()
        {
            var parameters = URLParameters.Parse("https://example.com/?profile=0x1234&emote=wave&urn=urnyurn&background=FF0000&contract=0x22334&item=0x5678&token=0x9ABC");
            
            Assert.AreEqual("0x1234", parameters.Profile);
            Assert.AreEqual("wave", parameters.Emote);
            Assert.AreEqual("urnyurn", parameters.Urn);
            Assert.AreEqual(Color.red, parameters.Background);
            Assert.AreEqual("0x22334", parameters.Contract);
            Assert.AreEqual("0x5678", parameters.ItemID);
            Assert.AreEqual("0x9ABC", parameters.TokenID);
        }
    }
}