using System;
using NUnit.Framework;
using UnityEngine;

namespace SheetSync.Tests
{
    /// <summary>
    /// テスト用のEnum定義
    /// </summary>
    public enum TestEnum
    {
        None = 0,
        Value1 = 1,
        Value2 = 2,
        Value3 = 3
    }

    /// <summary>
    /// Flags属性を持つテスト用Enum
    /// </summary>
    [Flags]
    public enum FighterType
    {
        None = 0,
        Soldier = 1,
        Archer = 2,
        Sorcerer = 4,
        ArcherAndSorcerer = Archer | Sorcerer,
        AllSoldier = Soldier | Archer | Sorcerer,
        Tap = 64,
        Monster = 128,
        AttackSkill = 256,
    }

    /// <summary>
    /// 別の名前空間のEnum（namespace無視テスト用）
    /// </summary>
    namespace OtherNamespace
    {
        public enum OtherEnum
        {
            First = 10,
            Second = 20,
            Third = 30
        }
    }

    public class Str2TypeConverterTests
    {
        [Test]
        public void ConvertInt_WithEnumString_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(int), "TestEnum.Value2");
            
            // Assert
            Assert.AreEqual(2, result);
        }

        [Test]
        public void ConvertInt_WithFlagsEnumString_ReturnsSingleValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(int), "FighterType.Soldier");
            
            // Assert
            Assert.AreEqual(1, result);
        }

        [Test]
        public void ConvertInt_WithFlagsEnumString_ReturnsCombinedValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(int), "FighterType.ArcherAndSorcerer");
            
            // Assert
            Assert.AreEqual(6, result); // 2 | 4 = 6
        }

        [Test]
        public void ConvertInt_WithInvalidEnumString_ReturnsNull()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(int), "NonExistentEnum.Value");
            
            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ConvertInt_WithInvalidEnumValue_ReturnsNull()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(int), "TestEnum.InvalidValue");
            
            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ConvertEnum_WithValidString_ReturnsEnumValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(TestEnum), "Value1");
            
            // Assert
            Assert.AreEqual(TestEnum.Value1, result);
        }

        [Test]
        public void ConvertFlagsEnum_WithSingleValue_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(FighterType), "Archer");
            
            // Assert
            Assert.AreEqual((int)FighterType.Archer, result);
        }

        [Test]
        public void ConvertFlagsEnum_WithCombinedValue_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(FighterType), "Archer | Sorcerer");
            
            // Assert
            Assert.AreEqual((int)(FighterType.Archer | FighterType.Sorcerer), result);
        }

        [Test]
        public void ConvertFlagsEnum_WithMultipleValues_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(FighterType), "Soldier | Archer | Sorcerer");
            
            // Assert
            Assert.AreEqual((int)FighterType.AllSoldier, result);
        }

        [Test]
        public void ConvertInt_WithNamespacedEnum_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(int), "OtherEnum.Second");
            
            // Assert
            Assert.AreEqual(20, result);
        }

        [Test]
        public void ConvertInt_WithUnityBuiltinEnum_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(int), "KeyCode.Tab");
            
            // Assert
            Assert.AreEqual((int)KeyCode.Tab, result);
        }

        [Test]
        public void ConvertInt_WithNumericString_ReturnsIntValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(int), "42");
            
            // Assert
            Assert.AreEqual(42, result);
        }

        [Test]
        public void ConvertEnum_WithInvalidValue_ReturnsNull()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(TestEnum), "NonExistentValue");
            
            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ConvertFloat_WithValidString_ReturnsFloatValue()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(float), "3.14");
            
            // Assert
            Assert.AreEqual(3.14f, result);
        }

        [Test]
        public void ConvertString_WithQuotedString_ReturnsUnquotedString()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(string), "\"Hello World\"");
            
            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [Test]
        public void ConvertVector3_WithValidString_ReturnsVector3()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(Vector3), "(1.0, 2.0, 3.0)");
            
            // Assert
            Assert.AreEqual(new Vector3(1.0f, 2.0f, 3.0f), result);
        }

        [Test]
        public void ConvertIntArray_WithValidString_ReturnsIntArray()
        {
            // Arrange & Act
            var result = Str2TypeConverter.Convert(typeof(int[]), "[1, 2, 3]");
            
            // Assert
            var array = result as int[];
            Assert.IsNotNull(array);
            Assert.AreEqual(3, array.Length);
            Assert.AreEqual(1, array[0]);
            Assert.AreEqual(2, array[1]);
            Assert.AreEqual(3, array[2]);
        }
    }
}