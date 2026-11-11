using NUnit.Framework;

namespace Clube.Core.Tests
{
    public class DailyQuotaTests
    {
        [Test]
        public void DayOneNeeds1024()
        {
            var quota = new DailyQuota();
            quota.Add(1, 1023);
            Assert.IsFalse(quota.IsMet);
            quota.Add(1, 1);
            Assert.IsTrue(quota.IsMet);
            Assert.AreEqual(1024, quota.Required);
        }

        [Test]
        public void DayTwoNeeds2048FromThatDayOnly()
        {
            var quota = new DailyQuota();
            quota.Add(1, 5000);
            quota.Advance(2);
            Assert.AreEqual(0, quota.Stone, "yesterday's stone doesn't count");
            Assert.AreEqual(2048, quota.Required);
            quota.Add(2, 2047);
            Assert.IsFalse(quota.IsMet);
            quota.Add(2, 1);
            Assert.IsTrue(quota.IsMet);
        }

        [Test]
        public void AddingOnANewDayStartsItsCount()
        {
            var quota = new DailyQuota();
            quota.Add(1, 900);
            quota.Add(3, 10);
            Assert.AreEqual(3, quota.Day);
            Assert.AreEqual(10, quota.Stone);
            Assert.AreEqual(4096, quota.Required);
        }

        [Test]
        public void RestoreKeepsDayAndCount()
        {
            var quota = new DailyQuota();
            quota.Restore(4, 77);
            quota.Advance(4);
            Assert.AreEqual(77, quota.Stone);
            Assert.AreEqual(8192, quota.Required);
        }
    }
}
