using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NJsonSchema.Generation;
using Xunit;

namespace NJsonSchema.Tests.Generation
{
    public class EnumGenerationTests
    {
        public class Foo
        {
            public Bar Bar { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public Bar Bar2 { get; set; }
        }

        public enum Bar
        {
            A = 0,
            B = 5,
            C = 6,
        }

        [Fact]
        public async Task When_property_is_integer_enum_then_schema_has_enum()
        {
            //// Arrange


            //// Act
            var schema = await JsonSchema4.FromTypeAsync<Foo>(new JsonSchemaGeneratorSettings
            {
                DefaultEnumHandling = EnumHandling.Integer
            });
            var data = schema.ToJson();

            //// Assert
            Assert.Equal(JsonObjectType.Integer, schema.Properties["Bar"].ActualPropertySchema.Type);
            Assert.Equal(3, schema.Properties["Bar"].ActualPropertySchema.Enumeration.Count);
            Assert.Equal(0, schema.Properties["Bar"].ActualPropertySchema.Enumeration.ElementAt(0));
            Assert.Equal(5, schema.Properties["Bar"].ActualPropertySchema.Enumeration.ElementAt(1));
            Assert.Equal(6, schema.Properties["Bar"].ActualPropertySchema.Enumeration.ElementAt(2));
        }

        [Fact]
        public async Task When_string_and_integer_enum_used_then_two_refs_are_generated()
        {
            //// Arrange


            //// Act
            var schema = await JsonSchema4.FromTypeAsync<Foo>(new JsonSchemaGeneratorSettings
            {
                DefaultEnumHandling = EnumHandling.Integer
            });
            var data = schema.ToJson();

            //// Assert
            Assert.NotNull(schema.Properties["Bar"].ActualPropertySchema);
            Assert.NotNull(schema.Properties["Bar2"].ActualPropertySchema); // must not be a reference but second enum declaration
            Assert.NotEqual(schema.Properties["Bar"].ActualPropertySchema, schema.Properties["Bar2"].ActualPropertySchema);
        }

        [Fact]
        public async Task When_property_is_string_enum_then_schema_has_enum()
        {
            //// Arrange


            //// Act
            var schema = await JsonSchema4.FromTypeAsync<Foo>(new JsonSchemaGeneratorSettings
            {
                DefaultEnumHandling = EnumHandling.String
            });

            //// Assert
            Assert.Equal(JsonObjectType.String, schema.Properties["Bar"].ActualPropertySchema.Type);
            Assert.Equal(3, schema.Properties["Bar"].ActualPropertySchema.Enumeration.Count);
            Assert.Equal("A", schema.Properties["Bar"].ActualPropertySchema.Enumeration.ElementAt(0));
            Assert.Equal("B", schema.Properties["Bar"].ActualPropertySchema.Enumeration.ElementAt(1));
            Assert.Equal("C", schema.Properties["Bar"].ActualPropertySchema.Enumeration.ElementAt(2));
        }

        [Fact]
        public async Task When_enum_is_generated_then_names_are_set()
        {
            //// Arrange


            //// Act
            var schema = await JsonSchema4.FromTypeAsync<Foo>(new JsonSchemaGeneratorSettings
            {
                DefaultEnumHandling = EnumHandling.Integer
            });

            //// Assert
            Assert.Equal(3, schema.Properties["Bar"].ActualPropertySchema.EnumerationNames.Count);
            Assert.Equal("A", schema.Properties["Bar"].ActualPropertySchema.EnumerationNames.ElementAt(0));
            Assert.Equal("B", schema.Properties["Bar"].ActualPropertySchema.EnumerationNames.ElementAt(1));
            Assert.Equal("C", schema.Properties["Bar"].ActualPropertySchema.EnumerationNames.ElementAt(2));
        }

        public class EnumProperty
        {
            [DefaultValue(Bar.C)]
            public Bar Bar { get; set; }
        }

        [Fact]
        public async Task When_enum_property_is_generated_then_enum_is_referenced()
        {
            //// Arrange


            //// Act
            var schema = await JsonSchema4.FromTypeAsync<EnumProperty>(new JsonSchemaGeneratorSettings
            {
                SchemaType = SchemaType.Swagger2,
                DefaultEnumHandling = EnumHandling.Integer
            });
            var json = schema.ToJson();

            //// Assert
            Assert.Equal(Bar.C, schema.Properties["Bar"].Default);
            Assert.True(schema.Properties["Bar"].HasReference);
        }
    }
}