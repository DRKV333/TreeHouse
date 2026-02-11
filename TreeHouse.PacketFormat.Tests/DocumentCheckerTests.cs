using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TreeHouse.PacketFormat.Tests;

[TestFixture]
public class DocumentCheckerTests
{
    [Test]
    public void DuplicatePacketNameTest()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Packets = new()
            {
                ["SomePacket"] = new PacketDefinition()
                {
                    Id = 1,
                    SubId = 2
                }
            }
        });

        checker.CheckDocument("b", new PacketFormatDocument()
        {
            Packets = new()
            {
                ["SomePacket"] = new PacketDefinition()
                {
                    Id = 3,
                    SubId = 4
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.DuplicatePacketName }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }
    

    [Test]
    public void DuplicateStructureName()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
            }
        });

        checker.CheckDocument("b", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.DuplicateStructureName }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public void DuplicatePacketId()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Packets = new()
            {
                ["SomePacket"] = new PacketDefinition()
                {
                    Id = 1,
                    SubId = 2
                }
            }
        });

        checker.CheckDocument("b", new PacketFormatDocument()
        {
            Packets = new()
            {
                ["OtherPacket"] = new PacketDefinition()
                {
                    Id = 1,
                    SubId = 2
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.DuplicatePacketId }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public void MultipleFieldDefinition()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
                {
                    Fields = new()
                    {
                        new Field() { Name = "SomeField", Type = new PrimitiveFieldType() { Value = "i32" } },
                        new Field() { Name = "SomeField", Type = new PrimitiveFieldType() { Value = "i32" } }
                    }
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.MultipleFieldDefinition }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public void EmptyBranch()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
                {
                    Fields = new()
                    {
                        new Field() { Name = "SomeField", Type = new PrimitiveFieldType() { Value = "bool" } },
                        new Branch() { Details = new BranchDetails() { Field = "SomeField" } }
                    }
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.EmptyBranch }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public void FieldTypeDifferentOnBranch()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
                {
                    Fields = new()
                    {
                        new Field() { Name = "SomeField", Type = new PrimitiveFieldType() { Value = "bool" } },
                        new Branch()
                        {
                            Details = new BranchDetails()
                            {
                                Field = "SomeField",
                                IsTrue = new FieldsList()
                                {
                                    Fields = new()
                                    {
                                        new Field() { Name = "OtherField", Type = new PrimitiveFieldType() { Value = "i32" } }
                                    }
                                },
                                IsFalse = new FieldsList()
                                {
                                    Fields = new()
                                    {
                                        new Field() { Name = "OtherField", Type = new PrimitiveFieldType() { Value = "i64" } }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.FieldTypeDifferentOnBranch }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public void ReferencedFieldDoesNotExist()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
                {
                    Fields = new()
                    {
                        new Branch()
                        {
                            Details = new BranchDetails()
                            {
                                Field = "SomeField",
                                IsTrue = new FieldsList()
                                {
                                    Fields = new()
                                    {
                                        new Field() { Name = "OtherField", Type = new PrimitiveFieldType() { Value = "i32" } }
                                    }
                                },
                                IsFalse = new FieldsList()
                                {
                                    Fields = new()
                                    {
                                        new Field() { Name = "OtherField", Type = new PrimitiveFieldType() { Value = "i32" } }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.ReferencedFieldDoesNotExist }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public void ReferencedFieldMightNotExist()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
                {
                    Fields = new()
                    {
                        new Field() { Name = "SomeField", Type = new PrimitiveFieldType() { Value = "bool" } },
                        new Branch()
                        {
                            Details = new BranchDetails()
                            {
                                Field = "SomeField",
                                IsTrue = new FieldsList()
                                {
                                    Fields = new()
                                    {
                                        new Field() { Name = "OtherField", Type = new PrimitiveFieldType() { Value = "i32" } }
                                    }
                                }
                            }
                        },
                        new Field()
                        {
                            Name = "Test",
                            Type = new ArrayFieldType()
                            {
                                Type = "bool",
                                Len = "OtherField"
                            }
                        }
                    }
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.ReferencedFieldNotDefinitelyDefined }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public void BranchIntegerNoCondition()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
                {
                    Fields = new()
                    {
                        new Field() { Name = "SomeField", Type = new PrimitiveFieldType() { Value = "i32" } },
                        new Branch()
                        {
                            Details = new BranchDetails()
                            {
                                Field = "SomeField",
                                IsTrue = new FieldsList()
                                {
                                    Fields = new()
                                    {
                                        new Field() { Name = "OtherField", Type = new PrimitiveFieldType() { Value = "i32" } }
                                    }
                                },
                                IsFalse = new FieldsList()
                                {
                                    Fields = new()
                                    {
                                        new Field() { Name = "OtherField", Type = new PrimitiveFieldType() { Value = "i32" } }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.BranchIntegerNoCondition }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public void BranchBadFieldType()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
                {
                    Fields = new()
                    {
                        new Field() { Name = "SomeField", Type = new PrimitiveFieldType() { Value = "cstring" } },
                        new Branch()
                        {
                            Details = new BranchDetails()
                            {
                                Field = "SomeField",
                                IsTrue = new FieldsList()
                                {
                                    Fields = new()
                                    {
                                        new Field() { Name = "OtherField", Type = new PrimitiveFieldType() { Value = "i32" } }
                                    }
                                },
                                IsFalse = new FieldsList()
                                {
                                    Fields = new()
                                    {
                                        new Field() { Name = "OtherField", Type = new PrimitiveFieldType() { Value = "i32" } }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.BranchBadFieldType }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public static void LengthBadFieldType()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
                {
                    Fields = new()
                    {
                        new Field() { Name = "SomeField", Type = new PrimitiveFieldType() { Value = "cstring" } },
                        new Field() { Name = "Array", Type = new ArrayFieldType() { Len = "SomeField", Type = "i32" } }
                    }
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.LengthBadFieldType }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public static void ReferencedStructDoesNotExist()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
                {
                    Fields = new()
                    {
                        new Field() { Name = "SomeField", Type = new PrimitiveFieldType() { Value = ":SomeOtherStruct" } },
                    }
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.ReferencedStructDoesNotExist }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public static void ReferencedPacketDoesNotExist()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Packets = new()
            {
                ["SomePacket"] = new PacketDefinition()
                {
                    Inherit = "SomeOtherPacket"
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.ReferencedPacketDoesNotExist }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    [Test]
    public static void EnumTypeBadType()
    {
        DocumentChecker checker = new();

        checker.CheckDocument("a", new PacketFormatDocument()
        {
            Structures = new()
            {
                ["SomeStruct"] = new FieldsList()
                {
                    Fields = new()
                    {
                        new Field() { Name = "SomeField", Type = new EnumFieldType() { Name = "nativeparam" } },
                    }
                }
            }
        });

        checker.CheckReferences();
        Assert.That(ErrorReasons(checker), Is.EquivalentTo(new[] { CheckerErrorReason.EnumTypeBadType }));
        TestContext.Out.WriteLine(checker.Errors.First());
    }

    private static IEnumerable<CheckerErrorReason> ErrorReasons(DocumentChecker checker) => checker.Errors.Select(x => x.Reason);
}
