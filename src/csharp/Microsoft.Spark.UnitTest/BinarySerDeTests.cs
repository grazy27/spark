// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Spark.Utils;
using Xunit;

namespace Microsoft.Spark.UnitTest;

#pragma warning disable SYSLIB0011 // Type or member is obsolete
[Collection("Spark Unit Tests")]
public class BinarySerDeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData("test")]
    [InlineData(3.14)]
    public void Serialize_ShouldWriteObjectToStream(object o)
    {
        var ms = new MemoryStream();

        BinarySerDe.Serialize(ms, o);
        ms.Position = 0;

        Assert.Equal(o, new BinaryFormatter().Deserialize(ms));
    }

    [Fact]
    public void Deserialize_ShouldReturnExpectedProperty_WhenTypeMatch()
    {
        var pet = new Dog { Name = "Garry" };
        var ms = new MemoryStream();
        new BinaryFormatter().Serialize(ms, pet);
        ms.Position = 0;

        var result = BinarySerDe.Deserialize<Dog>(ms);

        Assert.Equal(pet.Name, result.Name);
    }

    [Fact]
    public void Deserialize_ShouldThrowInvalidCasr_WhenTypeMismatch()
    {
        var pet = new Dog { Name = "Garry" };
        var ms = new MemoryStream();
        new BinaryFormatter().Serialize(ms, pet);
        ms.Position = 0;

        var action = () => BinarySerDe.Deserialize<Cat>(ms);

        Assert.Throws<InvalidCastException>(action);
    }


    [Serializable]
    private abstract class Animal : IEqualityComparer<Animal>
    {
        public string Name { get; set; }

        public bool Equals(Animal x, Animal y)
        {
            throw new System.NotImplementedException();
        }

        public int GetHashCode([DisallowNull] Animal obj)
        {
            throw new System.NotImplementedException();
        }
    }

    [Serializable]
    private class Cat : Animal { }

    [Serializable]
    private class Dog : Animal { }

#pragma warning restore SYSLIB0011 // Type or member is obsolete
}
