using Clube.Core;
using NUnit.Framework;

namespace Clube.Core.Tests
{
    public class BuildVersionTests
    {
        [Test]
        public void CommitCountAndHashFollowTheDemoNumber()
        {
            Assert.AreEqual("v0.2.1312 (e84d3c5)", BuildVersion.Format("0.2", "1312", "e84d3c5", null));
        }

        [Test]
        public void ATagWins()
        {
            Assert.AreEqual("v0.3-rc1 (e84d3c5)", BuildVersion.Format("0.2", "1312", "e84d3c5", "v0.3-rc1"));
        }

        [Test]
        public void WithoutGitTheProjectVersionAlone()
        {
            Assert.AreEqual("v0.2", BuildVersion.Format("0.2", null, null, null));
        }

        [Test]
        public void TheEditorShowsTheDemoNumber()
        {
            Assert.AreEqual("v0.2 · Editor", BuildVersion.ForEditor("0.2"));
        }
    }
}
