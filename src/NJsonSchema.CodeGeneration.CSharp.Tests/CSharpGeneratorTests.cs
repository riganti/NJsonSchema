﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema.Generation;
using Xunit;
using NJsonSchema.Annotations;

namespace NJsonSchema.CodeGeneration.Tests.CSharp
{
    public class CSharpGeneratorTests
    {
        [Fact]
        public async Task When_type_is_array_and_items_and_item_is_not_defined_then_any_array_is_generated()
        {
            //// Arrange
            var json = @"{
                'properties': {
                    'emptySchema': { 'type': 'array' }
                }
            }";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var settings = new CSharpGeneratorSettings { ClassStyle = CSharpClassStyle.Poco, Namespace = "ns", };
            var generator = new CSharpGenerator(schema, settings);
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public System.Collections.ObjectModel.ObservableCollection<object> EmptySchema { get; set; } = ", output);
        }

        class CustomPropertyNameGenerator : IPropertyNameGenerator
        {
            public string Generate(JsonProperty property)
            {
                return "MyCustom" + ConversionUtilities.ConvertToUpperCamelCase(property.Name, true);
            }
        }
        class CustomTypeNameGenerator : ITypeNameGenerator
        {
            public string Generate(JsonSchema4 schema, string typeNameHint, IEnumerable<string> reservedTypeNames)
            {
                return "MyCustomType" + ConversionUtilities.ConvertToUpperCamelCase(typeNameHint, true);
            }
        }

        public class Teacher : Person
        {
            public string Class { get; set; }
        }

        public class Person
        {
            [Required]
            public string FirstName { get; set; }

            [JsonProperty("lastName")]
            public string LastName { get; set; }

            [ReadOnly(true)]
            public DateTime Birthday { get; set; }

            public TimeSpan TimeSpan { get; set; }

            public TimeSpan? TimeSpanOrNull { get; set; }

            public Gender Gender { get; set; }

            public Gender? GenderOrNull { get; set; }

            public Address Address { get; set; }

            [CanBeNull]
            public Address AddressOrNull { get; set; }

            public List<string> Array { get; set; }

            public Dictionary<string, int> Dictionary { get; set; }
        }

        public enum Gender
        {
            Male,
            Female
        }

        public class Address
        {
            public string Street { get; set; }

            public string City { get; set; }
        }

        [Fact]
        public async Task When_property_name_is_created_by_custom_fun_then_attribute_is_correct()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<Teacher>();
            var schemaData = schema.ToJson();
            var settings = new CSharpGeneratorSettings();

            settings.TypeNameGenerator = new CustomTypeNameGenerator();
            settings.PropertyNameGenerator = new CustomPropertyNameGenerator();
            var generator = new CSharpGenerator(schema, settings);

            //// Act
            var output = generator.GenerateFile("Teacher");
            Console.WriteLine(output);

            //// Assert
            Assert.Contains(@"[Newtonsoft.Json.JsonProperty(""lastName""", output);
            Assert.Contains(@"public string MyCustomLastName", output);
            Assert.Contains(@"public partial class MyCustomTypeTeacher", output);
            Assert.Contains(@"public partial class MyCustomTypePerson", output);
        }

        [Fact]
        public async Task When_schema_contains_ref_to_definition_that_refs_another_definition_then_result_should_contain_correct_target_ref_type()
        {
            //// Arrange
            var schemaJson =
@"{
	'x-typeName': 'foo',
	'type': 'object',
	'definitions': {
		'pRef': {
			'type': 'object',
			'properties': {
				'pRef2': {
					'$ref': '#/definitions/pRef2'
				},
				
			}
		},
		'pRef2': {
			'type': 'string'
		}
	},
	'properties': {
		'pRefs': {
			'type': 'array',
			'items': {
				'$ref': '#/definitions/pRef'
			}
		}
	}
}";

            var schema = await JsonSchema4.FromJsonAsync(schemaJson);
            var settings = new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco
            };
            var gen = new CSharpGenerator(schema, settings);

            //// Act
            var output = gen.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public System.Collections.ObjectModel.ObservableCollection<PRef>", output);
        }

        [Fact]
        public async Task When_namespace_is_set_then_it_should_appear_in_output()
        {
            //// Arrange
            var generator = await CreateGeneratorAsync();

            //// Act
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("namespace MyNamespace", output);
            Assert.Contains("Dictionary<string, int>", output);
        }

        [Fact]
        public async Task When_POCO_is_set_then_auto_properties_are_generated()
        {
            //// Arrange
            var generator = await CreateGeneratorAsync();
            generator.Settings.ClassStyle = CSharpClassStyle.Poco;

            //// Act
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("{ get; set; }", output);
        }

        [Fact]
        public async Task When_property_name_does_not_match_property_name_then_attribute_is_correct()
        {
            //// Arrange
            var generator = await CreateGeneratorAsync();

            //// Act
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains(@"[Newtonsoft.Json.JsonProperty(""lastName""", output);
            Assert.Contains(@"public string LastName", output);
        }

        [Fact]
        public async Task When_property_is_timespan_than_csharp_timespan_is_used()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<Person>();
            var data = schema.ToJson();
            var generator = new CSharpGenerator(schema);

            //// Act
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains(@"public System.TimeSpan TimeSpan", output);
        }

        [Fact]
        public async Task When_allOf_contains_one_schema_then_csharp_inheritance_is_generated()
        {
            //// Arrange
            var generator = await CreateGeneratorAsync();

            //// Act
            var output = generator.GenerateFile("Teacher");

            //// Assert
            Assert.Contains(@"class Teacher : Person, ", output);
        }

        [Fact]
        public async Task When_enum_has_description_then_csharp_has_xml_comment()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<Teacher>();
            schema.AllOf.First().ActualSchema.Properties["Gender"].Description = "EnumDesc.";
            var generator = new CSharpGenerator(schema);

            //// Act
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains(@"/// <summary>EnumDesc.</summary>", output);
        }

        [Fact]
        public async Task When_class_has_description_then_csharp_has_xml_comment()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<Teacher>();
            schema.Description = "ClassDesc.";
            var generator = new CSharpGenerator(schema);

            //// Act
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains(@"/// <summary>ClassDesc.</summary>", output);
        }

        [Fact]
        public async Task When_property_has_description_then_csharp_has_xml_comment()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<Teacher>();
            schema.Properties["Class"].Description = "PropertyDesc.";
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings { ClassStyle = CSharpClassStyle.Poco });

            //// Act
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains(@"/// <summary>PropertyDesc.</summary>", output);
        }

        public class File
        {
            public byte[] Content { get; set; }
        }

        [Fact]
        public async Task Can_generate_type_from_string_property_with_byte_format()
        {
            // Arrange
            var schema = await JsonSchema4.FromTypeAsync<File>();
            var generator = new CSharpGenerator(schema);

            // Act
            var output = generator.GenerateFile("MyClass");

            // Assert
            Assert.Contains("public byte[] Content", output);
        }

        [Fact]
        public async Task Can_generate_type_from_string_property_with_base64_format()
        {
            // Arrange
            var schema = await JsonSchema4.FromTypeAsync<File>();
            schema.Properties["Content"].Format = "base64";
            var generator = new CSharpGenerator(schema);

            // Act
            var output = generator.GenerateFile("MyClass");

            // Assert
            Assert.Contains("public byte[] Content", output);
        }

        [Fact]
        public void When_name_contains_dash_then_it_is_converted_to_upper_case()
        {
            //// Arrange
            var schema = new JsonSchema4();
            schema.Properties["foo-bar"] = new JsonProperty
            {
                Type = JsonObjectType.String
            };

            var generator = new CSharpGenerator(schema);

            // Act
            var output = generator.GenerateFile("MyClass");

            // Assert
            Assert.Contains(@"[Newtonsoft.Json.JsonProperty(""foo-bar"", ", output);
            Assert.Contains(@"public string FooBar", output);
        }

        [Fact]
        public void When_type_name_is_missing_then_anonymous_name_is_generated()
        {
            //// Arrange
            var schema = new JsonSchema4();
            var generator = new CSharpGenerator(schema);

            // Act
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.DoesNotContain(@"class  :", output);
        }

        private static async Task<CSharpGenerator> CreateGeneratorAsync()
        {
            var schema = await JsonSchema4.FromTypeAsync<Teacher>();
            var schemaData = schema.ToJson();
            var settings = new CSharpGeneratorSettings();
            settings.Namespace = "MyNamespace";
            var generator = new CSharpGenerator(schema, settings);
            return generator;
        }


        private class ObjectTestClass
        {
            public object Foo { get; set; }
        }

        [Fact]
        public async Task When_property_is_object_then_any_type_is_generated()
        {
            //// Arrange

            //// Act
            var schema = await JsonSchema4.FromTypeAsync<ObjectTestClass>();

            //// Assert
            Assert.Equal(
@"{
  ""$schema"": ""http://json-schema.org/draft-04/schema#""
}".Replace("\r", string.Empty), schema.Properties["Foo"].ActualTypeSchema.ToJson().Replace("\r", string.Empty));
        }

        [Fact]
        public async Task When_property_is_object_then_object_property_is_generated()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<ObjectTestClass>();

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings { ClassStyle = CSharpClassStyle.Poco });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public object Foo { get; set; }", code);
        }

        public enum ConstructionCode
        {
            FIRE_RSTV = 0,
            FRAME = 1,
            JOIST_MAS = 2,
            NON_CBST = 3,
        }

        public class ClassWithDefaultEnumProperty
        {
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(CSharpGeneratorTests.ConstructionCode.NON_CBST)]
            public ConstructionCode ConstructionCode { get; set; }
        }

        [Fact]
        public async Task When_enum_property_has_default_and_int_serialization_then_correct_csharp_code_generated()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<ClassWithDefaultEnumProperty>();
            var schemaJson = schema.ToJson();

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                Namespace = "Foo"
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public ConstructionCode ConstructionCode { get; set; } = Foo.ConstructionCode.NON_CBST;", code);
        }

        [Fact]
        public async Task When_enum_property_has_default_and_string_serialization_then_correct_csharp_code_generated()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<ClassWithDefaultEnumProperty>(new JsonSchemaGeneratorSettings { DefaultEnumHandling = EnumHandling.String });
            var schemaJson = schema.ToJson();

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                Namespace = "Foo"
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public ConstructionCode ConstructionCode { get; set; } = Foo.ConstructionCode.NON_CBST;", code);
        }

        [Fact]
        public async Task When_enum_type_name_is_missing_then_default_value_is_still_correctly_set()
        {
            //// Arrange
            var schemaJson = @"{
  ""type"": ""object"",
  ""additionalProperties"": false,
  ""properties"": {
    ""ConstructionCode"": {
      ""type"": ""integer"",
      ""x-enumNames"": [
        ""FIRE_RSTV"",
        ""FRAME"",
        ""JOIST_MAS"",
        ""NON_CBST""
      ],
      ""enum"": [
        ""FIRE_RSTV"",
        ""FRAME"",
        ""JOIST_MAS"",
        ""NON_CBST""
      ],
      ""default"": ""JOIST_MAS""
    }
  }
}";
            var schema = await JsonSchema4.FromJsonAsync(schemaJson);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                Namespace = "Foo"
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public MyClassConstructionCode ConstructionCode { get; set; } = Foo.MyClassConstructionCode.JOIST_MAS;", code);
        }

        [Fact]
        public async Task When_property_has_same_name_as_class_then_it_is_renamed()
        {
            //// Arrange
            var schemaJson = @"{
  ""type"": ""object"",
  ""properties"": {
    ""Foo"": {
      ""type"": ""string""
    }
  }
}";
            var schema = await JsonSchema4.FromJsonAsync(schemaJson);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings { ClassStyle = CSharpClassStyle.Poco });
            var code = generator.GenerateFile("Foo");

            //// Assert
            Assert.Contains("[Newtonsoft.Json.JsonProperty(\"Foo\", Required = Newtonsoft.Json.Required.DisallowNull", code);
            Assert.Contains("public string Foo1 { get; set; }", code);
        }

        [Fact]
        public async Task When_patternProperties_is_set_with_string_value_type_then_correct_dictionary_is_generated()
        {
            //// Arrange
            var schemaJson = @"{
                ""properties"": {
                    ""dict"": {
                        ""type"": ""object"", 
                        ""additionalProperties"": false,
                        ""patternProperties"": {
                            ""^[a-zA-Z_$][a-zA-Z_$0-9]*$"": {
                                ""type"": ""string""
                            }
                        }
                    }
                }
            }";

            var schema = await JsonSchema4.FromJsonAsync(schemaJson);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings { ClassStyle = CSharpClassStyle.Poco });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public System.Collections.Generic.Dictionary<string, string> Dict { get; set; } = new System.Collections.Generic.Dictionary<string, string>();", code);
        }

        [Fact]
        public void When_object_has_generic_name_then_it_is_transformed()
        {
            //// Arrange
            var schema = new JsonSchema4();
            schema.Type = JsonObjectType.Object;
            schema.Properties["foo"] = new JsonProperty
            {
                Type = JsonObjectType.Number
            };

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings { ClassStyle = CSharpClassStyle.Poco });
            var code = generator.GenerateFile("Foo[Bar[Inner]]");

            //// Assert
            Assert.Contains("public partial class FooOfBarOfInner", code);
        }

        [JsonObject(MemberSerialization.OptIn)]
        [GeneratedCode("NJsonSchema", "3.4.6065.33501")]
        public partial class Person2
        {
            [JsonProperty("FirstName", Required = Required.Always)]
            [Required]
            public string FirstName { get; set; }

            [JsonProperty("MiddleName", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public string MiddleName { get; set; }

            [JsonProperty("Age", Required = Required.AllowNull)]
            public int? Age { get; set; }
        }

        [Fact]
        public async Task When_property_is_required_then_CSharp_code_is_correct()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<Person2>();
            var schemaJson = schema.ToJson();

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings { ClassStyle = CSharpClassStyle.Poco });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains(@"  ""required"": [
    ""FirstName"",
    ""Age""
  ],
  ""properties"": {
    ""FirstName"": {
      ""type"": ""string""
    },
    ""MiddleName"": {
      ""type"": ""string""
    },
    ""Age"": {
      ""type"": [
        ""integer"",
        ""null""
      ],
      ""format"": ""int32""
    }
  }".Replace("\r", string.Empty), schemaJson.Replace("\r", string.Empty));

            Assert.Contains(@"        [Newtonsoft.Json.JsonProperty(""FirstName"", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string FirstName { get; set; }
    
        [Newtonsoft.Json.JsonProperty(""MiddleName"", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string MiddleName { get; set; }
    
        [Newtonsoft.Json.JsonProperty(""Age"", Required = Newtonsoft.Json.Required.AllowNull)]
        public int? Age { get; set; }".Replace("\r", string.Empty), code.Replace("\r", string.Empty));
        }

        [Fact]
        public void When_array_property_is_required_or_not_then_the_code_has_correct_initializer()
        {
            //// Arrange
            var schema = new JsonSchema4
            {
                Properties =
                {
                    { "A", new JsonProperty
                        {
                            Type = JsonObjectType.Array,
                            Item = new JsonSchema4
                            {
                                Type = JsonObjectType.String
                            },
                            IsRequired = true
                        }
                    },
                    { "B", new JsonProperty
                        {
                            Type = JsonObjectType.Array,
                            Item = new JsonSchema4
                            {
                                Type = JsonObjectType.String
                            },
                            IsRequired = false
                        }
                    },
                }
            };

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public System.Collections.ObjectModel.ObservableCollection<string> A { get; set; } = new System.Collections.ObjectModel.ObservableCollection<string>();", code);
            Assert.DoesNotContain("public System.Collections.ObjectModel.ObservableCollection<string> B { get; set; } = new System.Collections.ObjectModel.ObservableCollection<string>();", code);
        }

        [Fact]
        public void When_dictionary_property_is_required_or_not_then_the_code_has_correct_initializer()
        {
            //// Arrange
            var schema = new JsonSchema4
            {
                Properties =
                {
                    { "A", new JsonProperty
                        {
                            Type = JsonObjectType.Object,
                            AdditionalPropertiesSchema = new JsonSchema4
                            {
                                Type = JsonObjectType.String
                            },
                            IsRequired = true
                        }
                    },
                    { "B", new JsonProperty
                        {
                            Type = JsonObjectType.Object,
                            AdditionalPropertiesSchema = new JsonSchema4
                            {
                                Type = JsonObjectType.String
                            },
                            IsRequired = false
                        }
                    },
                }
            };

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public System.Collections.Generic.Dictionary<string, string> A { get; set; } = new System.Collections.Generic.Dictionary<string, string>();", code);
            Assert.DoesNotContain("public System.Collections.Generic.Dictionary<string, string> B { get; set; } = new System.Collections.Generic.Dictionary<string, string>();", code);
        }

        [Fact]
        public void When_object_property_is_required_or_not_then_the_code_has_correct_initializer()
        {
            //// Arrange
            var schema = new JsonSchema4
            {
                Properties =
                {
                    { "A", new JsonProperty
                        {
                            Type = JsonObjectType.Object,
                            Properties =
                            {
                                {"A", new JsonProperty
                                    {
                                        Type = JsonObjectType.String
                                    }
                                }
                            },
                            IsRequired = true
                        }
                    },
                    { "B", new JsonProperty
                        {
                            Type = JsonObjectType.Object,
                            Properties =
                            {
                                {"A", new JsonProperty
                                    {
                                        Type = JsonObjectType.String
                                    }
                                }
                            },
                            IsRequired = false
                        }
                    },
                }
            };

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public A A { get; set; } = new A();", code);
            Assert.DoesNotContain("public B B { get; set; } = new B();", code);
        }

        [Fact]
        public async Task When_definition_is_named_Object_then_JObject_is_generated()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
			""$ref"": ""#/definitions/Object""
		}
	}, 
	""definitions"": {
		""Object"": { 
			""type"": ""object"", 
			""properties"": {} 
		}
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public object Foo { get; set; }", code);
        }

        public class ObsClass
        {
            public ObservableCollection<string> Test { get; set; }
        }

        [Fact]
        public async Task When_property_is_ObservableCollection_then_generated_code_uses_the_same_class()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<ObsClass>();
            var settings = new CSharpGeneratorSettings();
            var generator = new CSharpGenerator(schema, settings);

            //// Act
            var output = generator.GenerateFile("MyClass");
            Console.WriteLine(output);

            //// Assert
            Assert.Contains("ObservableCollection<string>", output);
        }

        [Fact]
        public async Task When_enum_has_special_chars_then_they_should_be_converted()
        {
            //// Arrange
            var schemaJson = @"{ ""type"": ""string"", ""enum"": [""application/json"",""application/vnd.ms-excel""] }";
            var schema = await JsonSchema4.FromJsonAsync(schemaJson);

            var settings = new CSharpGeneratorSettings();
            var generator = new CSharpGenerator(schema, settings);

            //// Act
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("Application_vnd_msExcel = 1,", output);
        }

        [Fact]
        public async Task When_property_has_not_supported_characters_then_they_are_removed()
        {
            //// Arrange
            var schemaJson =
@"{
  ""type"": ""object"",
  ""properties"": {
    ""@odata.context"": { ""type"": ""string"" }
  }
}";
            var schema = await JsonSchema4.FromJsonAsync(schemaJson);

            var settings = new CSharpGeneratorSettings();
            var generator = new CSharpGenerator(schema, settings);

            //// Act
            var output = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("public string OdataContext", output);
        }

        [Fact]
        public async Task When_definition_contains_minimum_a_range_attribute_is_added_with_minimum_and_max_double_maximum()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
		  ""type"": ""integer"",
		  ""minimum"": ""1""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("[System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]", code);
        }

        [Fact]
        public async Task When_definition_contains_maximum_a_range_attribute_is_added_with_min_double_minimum_and_maximum()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
		  ""type"": ""integer"",
		  ""maximum"": ""10""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("[System.ComponentModel.DataAnnotations.Range(int.MinValue, 10)]", code);
        }

        [Fact]
        public async Task When_definition_contains_both_minimum_and_maximum_a_range_attribute_is_added()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
		  ""type"": ""integer"",
		  ""minimum"": ""1"",
		  ""maximum"": ""10""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("[System.ComponentModel.DataAnnotations.Range(1, 10)]", code);
        }

        [Fact]
        public async Task When_definition_contains_maximum_a_range_attribute_is_not_added_for_anything_but_type_number_or_integer()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
		  ""type"": ""string"",
		  ""maximum"": ""10""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.DoesNotContain("System.ComponentModel.DataAnnotations.Range", code);
        }

        [Fact]
        public async Task When_definition_contains_min_length_a_string_length_attribute_is_added()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
		  ""type"": ""string"",
		  ""minLength"": ""10""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("[System.ComponentModel.DataAnnotations.StringLength(int.MaxValue, MinimumLength = 10)]", code);
        }

        [Fact]
        public async Task When_definition_contains_max_length_a_string_length_attribute_is_added()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
		  ""type"": ""string"",
		  ""maxLength"": ""20""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("[System.ComponentModel.DataAnnotations.StringLength(20)]", code);
        }

        [Fact]
        public async Task When_definition_contains_both_min_and_max_length_a_string_length_attribute_is_added()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
		  ""type"": ""string"",
		  ""minLength"": ""10"",
		  ""maxLength"": ""20""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains("[System.ComponentModel.DataAnnotations.StringLength(20, MinimumLength = 10)]", code);
        }

        [Fact]
        public async Task When_definition_contains_both_min_length_a_string_length_attribute_is_added_only_for_type_string()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
		  ""type"": ""number"",
		  ""minLength"": ""10"",
		  ""maxLength"": ""20""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.DoesNotContain("System.ComponentModel.DataAnnotations.StringLength", code);
        }

        [Fact]
        public async Task When_definition_contains_pattern_a_regular_expression_attribute_is_added()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
		  ""type"": ""string"",
		  ""pattern"": ""^[a-zA-Z''-'\\s]{1,40}$""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains(@"[System.ComponentModel.DataAnnotations.RegularExpression(@""^[a-zA-Z''-'\s]{1,40}$"")]", code);
        }

        [Fact]
        public async Task When_definition_contains_pattern_but_type_is_not_string_a_regular_expression_should_not_be_added()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""foo"": {
		  ""type"": ""number"",
		  ""pattern"": ""^[a-zA-Z''-'\\s]{1,40}$""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.DoesNotContain(@"System.ComponentModel.DataAnnotations.RegularExpression", code);
        }

        [Fact]
        public async Task When_definition_contains_restrictions_but_render_data_annotations_is_set_to_false_they_should_not_be_included()
        {
            //// Arrange
            var json =
                @"{
	""type"": ""object"", 
	""properties"": {
		""a"": {
		  ""type"": ""integer"",
		  ""minimum"": ""1"",
		  ""maximum"": ""10""
        },
		""b"": {
		  ""type"": ""string"",
		  ""minLength"": ""10"",
		  ""maxLength"": ""20""
        },
		""c"": {
		  ""type"": ""string"",
		  ""pattern"": ""^[a-zA-Z''-'\\s]{1,40}$""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2,
                // define that no data annotations should be included
                GenerateDataAnnotations = false
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.DoesNotContain(@"System.ComponentModel.DataAnnotations", code);
        }

        public class MyByteTest
        {
            public byte? Cell { get; set; }
        }

        [Fact]
        public async Task When_property_is_byte_then_its_type_is_preserved()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<MyByteTest>();
            var json = schema.ToJson();

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings { ClassStyle = CSharpClassStyle.Poco });
            var code = generator.GenerateFile();

            //// Assert
            Assert.Contains("public byte? Cell { get; set; }", code);
        }

        public class MyRequiredNullableTest
        {
            [Required]
            public int? Foo { get; set; }
        }

        [Fact]
        public async Task When_nullable_property_is_required_then_it_is_not_nullable_in_generated_csharp_code()
        {
            //// Arrange
            var schema = await JsonSchema4.FromTypeAsync<MyRequiredNullableTest>();
            var json = schema.ToJson();

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings { ClassStyle = CSharpClassStyle.Poco });
            var code = generator.GenerateFile();

            //// Assert
            Assert.Contains("public int Foo { get; set; }", code);
        }

        [Fact]
        public async Task When_definition_contains_date_converter_should_be_added_for_datetime()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""a"": {
    		""type"": ""string"",
            ""format"": ""date""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2,
                DateType = "System.DateTime"
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains(@"class DateFormatConverter", code);
            Assert.Contains(@"[Newtonsoft.Json.JsonConverter(typeof(DateFormatConverter))]", code);
	    }

        [Fact]
        public async Task When_definition_contains_date_converter_should_be_added_for_datetimeoffset()
        {
            //// Arrange
            var json =
@"{
	""type"": ""object"", 
	""properties"": {
		""a"": {
    		""type"": ""string"",
            ""format"": ""date""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2,
                DateType = "System.DateTimeOffset"
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.Contains(@"class DateFormatConverter", code);
            Assert.Contains(@"[Newtonsoft.Json.JsonConverter(typeof(DateFormatConverter))]", code);
        }

        [Fact]
        public async Task When_definition_contains_datetime_converter_should_not_be_added()
        {
            //// Arrange
            var json =
                @"{
	""type"": ""object"", 
	""properties"": {
		""a"": {
    		""type"": ""string"",
            ""format"": ""date-time""
        }
	}
}";
            var schema = await JsonSchema4.FromJsonAsync(json);

            //// Act
            var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings
            {
                ClassStyle = CSharpClassStyle.Poco,
                SchemaType = SchemaType.Swagger2,
                DateType = "System.DateTime"
            });
            var code = generator.GenerateFile("MyClass");

            //// Assert
            Assert.DoesNotContain(@"class DateFormatConverter", code);
            Assert.DoesNotContain(@"[Newtonsoft.Json.JsonConverter(typeof(DateFormatConverter))]", code);
        }
    }
}
