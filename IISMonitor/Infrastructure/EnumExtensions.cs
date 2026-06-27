using System;
using System.ComponentModel;
using System.Reflection;

namespace IISMonitor.Infrastructure
{
    /// <summary>
    /// 枚举扩展方法，用于获取 [Description] 特性的中文描述
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// 获取枚举值的中文描述（如 [Description("仅回收应用程序池")]）
        /// </summary>
        public static string GetDescription(this Enum value)
        {
            if (value == null) return string.Empty;

            var field = value.GetType().GetField(value.ToString());
            if (field == null) return value.ToString();

            var attr = field.GetCustomAttribute<DescriptionAttribute>();
            return attr != null && !string.IsNullOrEmpty(attr.Description)
                ? attr.Description
                : value.ToString();
        }

        /// <summary>
        /// 根据中文描述反查枚举值
        /// </summary>
        public static bool TryParseByDescription<TEnum>(string description, out TEnum result)
            where TEnum : struct, Enum
        {
            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                if (value.GetDescription() == description)
                {
                    result = value;
                    return true;
                }
            }
            result = default(TEnum);
            return false;
        }
    }
}
