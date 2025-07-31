using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SheetSync.Tests.TestTypes;

namespace SheetSync.Tests
{
    public class AssetsGeneratorServiceTests
    {
        private AssetsGeneratorService service;

        [SetUp]
        public void Setup()
        {
            service = new AssetsGeneratorService();
        }

        [Test]
        public void ConvertCsvValueToFieldType_IntField_ReturnsInt()
        {
            // Arrange & Act
            var result = service.ConvertCsvValueToFieldType(typeof(int), "42");

            // Assert
            Assert.AreEqual(42, result);
        }

        [Test]
        public void ConvertCsvValueToFieldType_StringField_ReturnsString()
        {
            // Arrange & Act
            var result = service.ConvertCsvValueToFieldType(typeof(string), "Hello World");

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [Test]
        public void ConvertCsvValueToFieldType_HumanTypeField_ReturnsEnum()
        {
            // Arrange & Act
            var result = service.ConvertCsvValueToFieldType(typeof(HumanType), "Player");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual((int)HumanType.Player, result);
        }

        [Test]
        public void ConvertCsvValueToFieldType_HumanTypeFlagsField_ReturnsEnum()
        {
            // Arrange & Act
            var result = service.ConvertCsvValueToFieldType(typeof(HumanType), "Player | Enemy");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual((int)(HumanType.Player | HumanType.Enemy), result);
        }

        [Test]
        public void ConvertCsvValueToFieldType_EmptyValue_ReturnsNull()
        {
            // Arrange & Act
            var result = service.ConvertCsvValueToFieldType(typeof(int), "");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void SetFieldValue_SimpleField_SetsValue()
        {
            // Arrange
            var testObject = new TestClass();
            var fieldInfo = typeof(TestClass).GetField("intValue");
            
            // Act
            service.SetFieldValue(testObject, fieldInfo, 100, false);

            // Assert
            Assert.AreEqual(100, testObject.intValue);
        }

        [Test]
        public void SetFieldValue_EnumField_SetsEnumValue()
        {
            // Arrange
            var testObject = new TestClass();
            var fieldInfo = typeof(TestClass).GetField("humanType");
            var enumValue = Enum.ToObject(typeof(HumanType), (int)HumanType.Enemy);
            
            // Act
            service.SetFieldValue(testObject, fieldInfo, enumValue, false);

            // Assert
            Assert.AreEqual(HumanType.Enemy, testObject.humanType);
        }

        [Test]
        public void SetFieldValue_EnumFieldWithIntValue_ConvertsToEnumType()
        {
            // Arrange
            var testObject = new TestClass();
            var fieldInfo = typeof(TestClass).GetField("humanType");
            int intValue = (int)HumanType.Player;
            
            // Act
            service.SetFieldValue(testObject, fieldInfo, intValue, false);

            // Assert
            Assert.AreEqual(HumanType.Player, testObject.humanType);
        }

        [Test]
        public void SetFieldValue_EnumFieldWithCombinedIntValue_ConvertsToEnumType()
        {
            // Arrange
            var testObject = new TestClass();
            var fieldInfo = typeof(TestClass).GetField("humanType");
            int intValue = (int)(HumanType.Player | HumanType.Enemy);
            
            // Act
            service.SetFieldValue(testObject, fieldInfo, intValue, false);

            // Assert
            Assert.AreEqual(HumanType.Player | HumanType.Enemy, testObject.humanType);
        }

        [Test]
        public void InitializeArrayField_CreatesEmptyArray()
        {
            // Arrange
            var testObject = new TestClass();
            var fieldInfo = typeof(TestClass).GetField("intArray");

            // Act
            service.InitializeArrayField(testObject, fieldInfo);

            // Assert
            Assert.IsNotNull(testObject.intArray);
            Assert.AreEqual(0, testObject.intArray.Length);
        }

        [Test]
        public void SetFieldValue_ArrayField_AppendsValue()
        {
            // Arrange
            var testObject = new TestClass();
            var fieldInfo = typeof(TestClass).GetField("intArray");
            service.InitializeArrayField(testObject, fieldInfo);

            // Act
            service.SetFieldValue(testObject, fieldInfo, 10, true);
            service.SetFieldValue(testObject, fieldInfo, 20, true);

            // Assert
            Assert.AreEqual(2, testObject.intArray.Length);
            Assert.AreEqual(10, testObject.intArray[0]);
            Assert.AreEqual(20, testObject.intArray[1]);
        }

        // テスト用のクラス
        public class TestClass
        {
            public int intValue;
            public string stringValue;
            public HumanType humanType;
            public int[] intArray;
        }
    }
}