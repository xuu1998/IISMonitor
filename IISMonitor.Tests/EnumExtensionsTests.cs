using System;
using System.ComponentModel;
using NUnit.Framework;
using IISMonitor.Infrastructure;

namespace IISMonitor.Tests
{
    [TestFixture]
    public class EnumExtensionsTests
    {
        // 内部测试枚举
        private enum TestEnum
        {
            [System.ComponentModel.Description("测试值 A")]
            ValueA,

            [System.ComponentModel.Description("测试值 B")]
            ValueB,

            // 没有 Description 特性的值
            NoDescription
        }

        [Test]
        public void GetDescription_WithDescription_ReturnsChineseText()
        {
            Assert.AreEqual("测试值 A", TestEnum.ValueA.GetDescription());
            Assert.AreEqual("测试值 B", TestEnum.ValueB.GetDescription());
        }

        [Test]
        public void GetDescription_NoDescription_FallsBackToEnumName()
        {
            Assert.AreEqual("NoDescription", TestEnum.NoDescription.GetDescription());
        }

        [Test]
        public void GetDescription_NullValue_ReturnsEmpty()
        {
            TestEnum? nullValue = null;
            Assert.AreEqual(string.Empty, nullValue.GetDescription());
        }

        [Test]
        public void TryParseByDescription_ValidValue_ReturnsTrueAndEnum()
        {
            bool result = EnumExtensions.TryParseByDescription<TestEnum>("测试值 A", out TestEnum value);

            Assert.IsTrue(result);
            Assert.AreEqual(TestEnum.ValueA, value);
        }

        [Test]
        public void TryParseByDescription_InvalidValue_ReturnsFalseAndDefault()
        {
            bool result = EnumExtensions.TryParseByDescription<TestEnum>("不存在的值", out TestEnum value);

            Assert.IsFalse(result);
            Assert.AreEqual(default(TestEnum), value);
        }

        [Test]
        public void TryParseByDescription_EmptyString_ReturnsFalse()
        {
            bool result = EnumExtensions.TryParseByDescription<TestEnum>("", out TestEnum value);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryParseByDescription_NullString_ReturnsFalse()
        {
            bool result = EnumExtensions.TryParseByDescription<TestEnum>(null, out TestEnum value);

            Assert.IsFalse(result);
        }

        [Test]
        public void GetDescription_OnRestartStrategy_AllHaveNonEmptyDescription()
        {
            foreach (RestartStrategyType value in Enum.GetValues(typeof(RestartStrategyType)))
            {
                var desc = value.GetDescription();
                Assert.IsFalse(string.IsNullOrWhiteSpace(desc),
                    $"{value} 的中文描述不应为空");
            }
        }
    }
}
