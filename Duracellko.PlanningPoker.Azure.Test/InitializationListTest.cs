﻿// <copyright>
// Copyright (c) 2012 Rasto Novotny
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Duracellko.PlanningPoker.Azure.Test
{
    [TestClass]
    public class InitializationListTest
    {
        #region IsEmpty

        [TestMethod]
        public void IsEmpty_NewInstance_True()
        {
            // Arrange
            var target = new InitializationList();

            // Act
            var result = target.IsEmpty;

            // Verify
            Assert.IsFalse(result);
        }

        #endregion

        #region Clear

        [TestMethod]
        public void Values_NewInstance_Null()
        {
            // Arrange
            var target = new InitializationList();

            // Act
            var result = target.Values;

            // Verify
            Assert.IsNull(result);
        }

        #endregion

        #region ContainsOrNotInit

        [TestMethod]
        public void ContainsOrNotInit_ExistingValue_ReturnsTrue()
        {
            // Arrange
            var target = new InitializationList();
            target.Setup(new string[] { "team1", "team2" });

            // Act
            var result = target.ContainsOrNotInit("team2");

            // Verify
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ContainsOrNotInit_NonexistingValue_ReturnsFalse()
        {
            // Arrange
            var target = new InitializationList();
            target.Setup(new string[] { "team1", "team2" });

            // Act
            var result = target.ContainsOrNotInit("team3");

            // Verify
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ContainsOrNotInit_NotInitialized_ReturnsTrue()
        {
            // Arrange
            var target = new InitializationList();

            // Act
            var result = target.ContainsOrNotInit("team2");

            // Verify
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ContainsOrNotInit_Empty_ReturnsFalse()
        {
            // Arrange
            var target = new InitializationList();
            target.Clear();

            // Act
            var result = target.ContainsOrNotInit("team3");

            // Verify
            Assert.IsFalse(result);
        }

        #endregion

        #region Setup

        [TestMethod]
        public void Setup_2ValuesAndNotInitialized_ValuesAreSet()
        {
            // Arrange
            var target = new InitializationList();

            // Act
            target.Setup(new string[] { "team1", "team2" });

            // Verify
            Assert.IsFalse(target.IsEmpty);
            Assert.IsNotNull(target.Values);
            CollectionAssert.AreEquivalent(new string[] { "team1", "team2" }, target.Values.ToList());
        }

        [TestMethod]
        public void Setup_2ValuesAndNotInitialized_ReturnsTrue()
        {
            // Arrange
            var target = new InitializationList();

            // Act
            var result = target.Setup(new string[] { "team1", "team2" });

            // Verify
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Setup_2ValuesAndInitializedAlready_ValuesAreNotSet()
        {
            // Arrange
            var target = new InitializationList();
            target.Setup(new string[] { "team1" });

            // Act
            target.Setup(new string[] { "team3", "team4" });

            // Verify
            Assert.IsFalse(target.IsEmpty);
            Assert.IsNotNull(target.Values);
            CollectionAssert.AreEquivalent(new string[] { "team1" }, target.Values.ToList());
        }

        [TestMethod]
        public void Setup_2ValuesAndInitializedAlready_ReturnsFalse()
        {
            // Arrange
            var target = new InitializationList();
            target.Setup(new string[] { "team1" });

            // Act
            var result = target.Setup(new string[] { "team3", "team4" });

            // Verify
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Setup_2ValuesAndClearedAlready_ValuesAreNotSet()
        {
            // Arrange
            var target = new InitializationList();
            target.Clear();

            // Act
            target.Setup(new string[] { "team3", "team4" });

            // Verify
            Assert.IsTrue(target.IsEmpty);
            Assert.IsNotNull(target.Values);
            CollectionAssert.AreEquivalent(new string[0], target.Values.ToList());
        }

        [TestMethod]
        public void Setup_2ValuesAndClearedAlready_ReturnsFalse()
        {
            // Arrange
            var target = new InitializationList();
            target.Clear();

            // Act
            var result = target.Setup(new string[] { "team3", "team4" });

            // Verify
            Assert.IsFalse(result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Setup_Null_ArgumentNullException()
        {
            // Arrange
            var target = new InitializationList();

            // Act
            target.Setup(null);
        }

        #endregion

        #region Remove

        [TestMethod]
        public void Remove_ExistingValue_ValueNotInCollection()
        {
            // Arrange
            var target = new InitializationList();
            target.Setup(new string[] { "team1", "team2" });

            // Act
            target.Remove("team2");

            // Verify
            CollectionAssert.AreEquivalent(new string[] { "team1" }, target.Values.ToList());
            Assert.IsFalse(target.IsEmpty);
        }

        [TestMethod]
        public void Remove_OnlyValue_CollectionIsEmpty()
        {
            // Arrange
            var target = new InitializationList();
            target.Setup(new string[] { "team2", "team2" });

            // Act
            target.Remove("team2");

            // Verify
            CollectionAssert.AreEquivalent(new string[0], target.Values.ToList());
            Assert.IsTrue(target.IsEmpty);
        }

        [TestMethod]
        public void Remove_ExistingValue_ReturnsTrue()
        {
            // Arrange
            var target = new InitializationList();
            target.Setup(new string[] { "team1", "team2" });

            // Act
            var result = target.Remove("team2");

            // Verify
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Remove_NonexistingValue_CollectionIsSame()
        {
            // Arrange
            var target = new InitializationList();
            target.Setup(new string[] { "team1", "team2" });

            // Act
            target.Remove("team3");

            // Verify
            CollectionAssert.AreEquivalent(new string[] { "team1", "team2" }, target.Values.ToList());
            Assert.IsFalse(target.IsEmpty);
        }

        [TestMethod]
        public void Remove_NonexistingValue_ReturnsFalse()
        {
            // Arrange
            var target = new InitializationList();
            target.Setup(new string[] { "team1", "team2" });

            // Act
            var result = target.Remove("team3");

            // Verify
            Assert.IsFalse(result);
        }

        #endregion

        #region Clear

        [TestMethod]
        public void Clear_AfterInitialization_IsEmpty()
        {
            // Arrange
            var target = new InitializationList();
            target.Setup(new string[] { "team1", "team2" });

            // Act
            target.Clear();

            // Verify
            Assert.IsTrue(target.IsEmpty);
            CollectionAssert.AreEquivalent(new string[0], target.Values.ToList());
        }

        [TestMethod]
        public void Clear_NoInitialization_IsEmpty()
        {
            // Arrange
            var target = new InitializationList();

            // Act
            target.Clear();

            // Verify
            Assert.IsTrue(target.IsEmpty);
            CollectionAssert.AreEquivalent(new string[0], target.Values.ToList());
        }

        #endregion
    }
}
