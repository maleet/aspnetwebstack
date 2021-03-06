﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Web.Http.OData.Builder;
using System.Web.Http.Routing;
using Microsoft.Data.Edm;
using Microsoft.Data.Edm.Annotations;
using Microsoft.Data.Edm.Library;
using Microsoft.Data.OData;
using Microsoft.TestCommon;
using Moq;

namespace System.Web.Http.OData.Formatter.Serialization
{
    public class ODataEntityTypeSerializerTests
    {
        IEdmModel _model;
        IEdmEntitySet _customerSet;
        Customer _customer;
        ODataEntityTypeSerializer _serializer;
        ODataSerializerContext _writeContext;
        EntityInstanceContext _entityInstanceContext;

        public ODataEntityTypeSerializerTests()
        {
            _model = SerializationTestsHelpers.SimpleCustomerOrderModel();

            _model.SetAnnotationValue<ClrTypeAnnotation>(_model.FindType("Default.Customer"), new ClrTypeAnnotation(typeof(Customer)));
            _model.SetAnnotationValue<ClrTypeAnnotation>(_model.FindType("Default.Order"), new ClrTypeAnnotation(typeof(Order)));

            _customerSet = _model.FindDeclaredEntityContainer("Default.Container").FindEntitySet("Customers");
            _customer = new Customer()
            {
                FirstName = "Foo",
                LastName = "Bar",
                ID = 10,
            };

            ODataSerializerProvider serializerProvider = new DefaultODataSerializerProvider();
            _serializer = new ODataEntityTypeSerializer(
                new EdmEntityTypeReference(_customerSet.ElementType, isNullable: false),
                serializerProvider);
            _writeContext = new ODataSerializerContext() { EntitySet = _customerSet, Model = _model };
            _entityInstanceContext = new EntityInstanceContext { EdmModel = _model, EntitySet = _customerSet, EntityInstance = _customer };
        }

        [Fact]
        public void Ctor_ThrowsArgumentNull_EdmType()
        {
            Assert.ThrowsArgumentNull(
                () => new ODataEntityTypeSerializer(edmType: null, serializerProvider: null),
                "edmType");
        }

        [Fact]
        public void Ctor_ThrowsArgumentNull_SerializerProvider()
        {
            Assert.ThrowsArgumentNull(
                () => new ODataEntityTypeSerializer(edmType: new Mock<IEdmEntityTypeReference>().Object, serializerProvider: null),
                "serializerProvider");
        }

        [Fact]
        public void Ctor_SetsProperty_EntityType()
        {
            IEdmEntityTypeReference entityType = new Mock<IEdmEntityTypeReference>().Object;
            ODataEntityTypeSerializer serializer = new ODataEntityTypeSerializer(entityType, new DefaultODataSerializerProvider());
            Assert.Equal(entityType, serializer.EntityType);
        }

        [Fact]
        public void WriteObject_ThrowsArgumentNull_MessageWriter()
        {
            Assert.ThrowsArgumentNull(
                () => _serializer.WriteObject(graph: new object(), messageWriter: null, writeContext: null),
                "messageWriter");
        }

        [Fact]
        public void WriteObject_ThrowsArgumentNull_WriteContext()
        {
            ODataMessageWriter messageWriter = new ODataMessageWriter(new Mock<IODataRequestMessage>().Object);
            Assert.ThrowsArgumentNull(
                () => _serializer.WriteObject(graph: new object(), messageWriter: messageWriter, writeContext: null),
                "writeContext");
        }

        [Fact]
        public void WriteObject_ThrowsSerializationException_WhenEntitySetIsMissingInWriteContext()
        {
            ODataMessageWriter messageWriter = new ODataMessageWriter(new Mock<IODataRequestMessage>().Object);
            Assert.Throws<SerializationException>(
                () => _serializer.WriteObject(graph: new object(), messageWriter: messageWriter, writeContext: new ODataSerializerContext()),
                "The related entity set could not be found from the OData path. The related entity set is required to serialize the payload.");
        }

        [Fact]
        public void WriteObject_Calls_WriteObjectInline()
        {
            // Arrange
            object graph = new object();
            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EntityType, new DefaultODataSerializerProvider());
            serializer.Setup(s => s.WriteObjectInline(graph, It.IsAny<ODataWriter>(), _writeContext)).Verifiable();
            serializer.CallBase = true;

            // Act
            serializer.Object.WriteObject(graph, ODataTestUtil.GetMockODataMessageWriter(), _writeContext);

            // Assert
            serializer.Verify();
        }

        [Fact]
        public void WriteObjectInline_ThrowsArgumentNull_Writer()
        {
            Assert.ThrowsArgumentNull(
                () => _serializer.WriteObjectInline(graph: null, writer: null, writeContext: new ODataSerializerContext()),
                "writer");
        }

        [Fact]
        public void WriteObjectInline_ThrowsArgumentNull_WriteContext()
        {
            Assert.ThrowsArgumentNull(
                () => _serializer.WriteObjectInline(graph: null, writer: new Mock<ODataWriter>().Object, writeContext: null),
                "writeContext");
        }

        [Fact]
        public void WriteObjectInline_ThrowsSerializationException_WhenGraphIsNull()
        {
            ODataWriter messageWriter = new Mock<ODataWriter>().Object;
            Assert.Throws<SerializationException>(
                () => _serializer.WriteObjectInline(graph: null, writer: messageWriter, writeContext: new ODataSerializerContext()),
                "Cannot serialize a null 'entry'.");
        }

        [Fact]
        public void WriteObjectInline_Calls_CreateEntry()
        {
            // Arrange
            var entityInstance = new object();
            ODataSerializerProvider serializerProvider = new DefaultODataSerializerProvider();
            ODataWriter writer = new Mock<ODataWriter>().Object;
            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EdmType, serializerProvider);
            serializer.CallBase = true;
            _writeContext.Request = new HttpRequestMessage();
            _writeContext.Url = new UrlHelper(_writeContext.Request);
            serializer
                .Setup(s => s.CreateEntry(It.IsAny<EntityInstanceContext>(), _writeContext))
                .Callback((EntityInstanceContext instanceContext, ODataSerializerContext writeContext) =>
                    {
                        VerifyEntityInstanceContext(instanceContext, writeContext);
                        Assert.Equal(entityInstance, instanceContext.EntityInstance);
                        Assert.Equal(serializer.Object.EdmType.Definition, instanceContext.EntityType);
                    });

            // Act
            serializer.Object.WriteObjectInline(entityInstance, writer, _writeContext);

            // Assert
            serializer.VerifyAll();
        }

        [Fact]
        public void WriteObjectInline_WritesODataEntryFrom_CreateEntry()
        {
            // Arrange
            ODataEntry entry = new ODataEntry();
            Mock<ODataWriter> writer = new Mock<ODataWriter>();
            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EdmType, new DefaultODataSerializerProvider());

            writer.Setup(s => s.WriteStart(entry)).Verifiable();
            serializer.Setup(s => s.CreateEntry(It.IsAny<EntityInstanceContext>(), _writeContext)).Returns(entry);
            serializer.CallBase = true;

            // Act
            serializer.Object.WriteObjectInline(new object(), writer.Object, _writeContext);

            // Assert
            writer.Verify();
        }

        [Fact]
        public void WriteObjectInline_Calls_CreateNavigationLinks()
        {
            // Arrange
            var entityInstance = new object();
            ODataSerializerProvider serializerProvider = new DefaultODataSerializerProvider();
            ODataWriter writer = new Mock<ODataWriter>().Object;
            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EdmType, serializerProvider);
            serializer.CallBase = true;
            _writeContext.Request = new HttpRequestMessage();
            _writeContext.Url = new UrlHelper(_writeContext.Request);
            serializer.Setup(s => s.CreateEntry(It.IsAny<EntityInstanceContext>(), _writeContext)).Returns(new ODataEntry());
            serializer
                .Setup(s => s.CreateNavigationLinks(It.IsAny<EntityInstanceContext>(), _writeContext))
                .Callback((EntityInstanceContext instanceContext, ODataSerializerContext writeContext) =>
                {
                    VerifyEntityInstanceContext(instanceContext, writeContext);
                    Assert.Equal(entityInstance, instanceContext.EntityInstance);
                    Assert.Equal(serializer.Object.EdmType.Definition, instanceContext.EntityType);
                });

            // Act
            serializer.Object.WriteObjectInline(entityInstance, writer, _writeContext);

            // Assert
            serializer.VerifyAll();
        }

        [Fact]
        public void WriteObjectInline_WritesODataNavigationLinksFrom_CreateNavigationLinks()
        {
            // Arrange
            ODataNavigationLink[] navigationLinks = new[] { new ODataNavigationLink(), new ODataNavigationLink() };
            ODataEntry entry = new ODataEntry();
            Mock<ODataWriter> writer = new Mock<ODataWriter>();
            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EdmType, new DefaultODataSerializerProvider());

            writer.Setup(s => s.WriteStart(navigationLinks[0])).Verifiable();
            writer.Setup(s => s.WriteStart(navigationLinks[1])).Verifiable();
            serializer.Setup(s => s.CreateEntry(It.IsAny<EntityInstanceContext>(), _writeContext)).Returns(new ODataEntry());
            serializer.Setup(s => s.CreateNavigationLinks(It.IsAny<EntityInstanceContext>(), _writeContext)).Returns(navigationLinks);
            serializer.CallBase = true;

            // Act
            serializer.Object.WriteObjectInline(new object(), writer.Object, _writeContext);

            // Assert
            writer.Verify();
        }

        [Fact]
        public void CreateEntry_Calls_CreateStructuralPropertyBag()
        {
            // Arrange
            ODataProperty[] properties = new ODataProperty[] { new ODataProperty(), new ODataProperty() };
            EntityInstanceContext entityInstanceContext = new EntityInstanceContext();
            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EdmType, new DefaultODataSerializerProvider());

            serializer.CallBase = true;
            serializer.Setup(s => s.CreateODataActions(entityInstanceContext, _writeContext)).Returns(Enumerable.Empty<ODataAction>());
            serializer.Setup(s => s.CreateStructuralPropertyBag(entityInstanceContext, _writeContext)).Returns(properties).Verifiable();

            // Act
            ODataEntry entry = serializer.Object.CreateEntry(entityInstanceContext, _writeContext);

            // Assert
            serializer.Verify();
            Assert.Same(properties, entry.Properties);
        }

        [Fact]
        public void CreateEntry_Calls_CreateODataActions()
        {
            // Arrange
            ODataAction[] actions = new ODataAction[] { new ODataAction(), new ODataAction() };
            EntityInstanceContext entityInstanceContext = new EntityInstanceContext();
            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EdmType, new DefaultODataSerializerProvider());

            serializer.CallBase = true;
            serializer.Setup(s => s.CreateODataActions(entityInstanceContext, _writeContext)).Returns(actions).Verifiable();
            serializer.Setup(s => s.CreateStructuralPropertyBag(entityInstanceContext, _writeContext));

            // Act
            ODataEntry entry = serializer.Object.CreateEntry(entityInstanceContext, _writeContext);

            // Assert
            serializer.Verify();
            Assert.Same(actions, entry.Actions);
        }

        [Fact]
        public void CreateNavigationLinks_ThrowsArgumentNull_WriteContext()
        {
            Assert.ThrowsArgumentNull(
                () => _serializer.CreateNavigationLinks(entityInstanceContext: null, writeContext: null).GetEnumerator().MoveNext(),
                "writeContext");
        }

        [Fact]
        public void CreateNavigationLinks_Returns_NavigationLinkForEachNaviagationProperty()
        {
            // Arrange
            IEdmNavigationProperty property1 = CreateFakeNavigationProperty("Property1", _serializer.EntityType);
            IEdmNavigationProperty property2 = CreateFakeNavigationProperty("Property2", _serializer.EntityType);
            Mock<IEdmEntityType> entityType = new Mock<IEdmEntityType>();
            entityType.Setup(e => e.DeclaredProperties).Returns(new[] { property1, property2 });

            var serializer = new ODataEntityTypeSerializer(new EdmEntityTypeReference(entityType.Object, isNullable: false), new DefaultODataSerializerProvider());

            MockEntitySetLinkBuilderAnnotation linkBuilder = new MockEntitySetLinkBuilderAnnotation
            {
                NavigationLinkBuilder = (ctxt, property, metadataLevel) => new Uri(property.Name, UriKind.Relative)
            };
            _model.SetEntitySetLinkBuilderAnnotation(_customerSet, linkBuilder);

            // Act
            IEnumerable<ODataNavigationLink> links = serializer.CreateNavigationLinks(new EntityInstanceContext(), _writeContext);

            // Assert
            Assert.Equal(new[] { "Property1", "Property2" }, links.Select(l => l.Name));
            Assert.Equal(new[] { "Property1", "Property2" }, links.Select(l => l.Url.ToString()));
            Assert.Equal(new bool?[] { false, false }, links.Select(l => l.IsCollection));
        }

        [Fact]
        public void CreateStructuralPropertyBag_ThrowsArgumentNull_EntityInstanceContext()
        {
            Assert.ThrowsArgumentNull(
                () => _serializer.CreateStructuralPropertyBag(entityInstanceContext: null, writeContext: null),
                "entityInstanceContext");
        }

        [Fact]
        public void CreateStructuralPropertyBag_Calls_CreateStructuralProperty_ForAllStructuralProperties()
        {
            // Arrange
            var instance = new object();
            IEdmStructuralProperty[] structuralProperties = new[] { new Mock<IEdmStructuralProperty>().Object, new Mock<IEdmStructuralProperty>().Object };
            ODataProperty[] oDataProperties = new[] { new ODataProperty(), new ODataProperty() };
            ODataAction[] actions = new ODataAction[] { new ODataAction(), new ODataAction() };
            EntityInstanceContext entityInstanceContext = new EntityInstanceContext { EntityInstance = instance };
            Mock<IEdmEntityType> entityType = new Mock<IEdmEntityType>();
            entityType.Setup(e => e.DeclaredProperties).Returns(structuralProperties);
            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(
                new EdmEntityTypeReference(entityType.Object, isNullable: false),
                new DefaultODataSerializerProvider());
            serializer.CallBase = true;

            serializer.Setup(s => s.CreateStructuralProperty(structuralProperties[0], instance, _writeContext)).Returns(oDataProperties[0]).Verifiable();
            serializer.Setup(s => s.CreateStructuralProperty(structuralProperties[1], instance, _writeContext)).Returns(oDataProperties[1]).Verifiable();

            // Act
            IEnumerable<ODataProperty> propertyValues = serializer.Object.CreateStructuralPropertyBag(entityInstanceContext, _writeContext);

            // Assert
            serializer.Verify();
        }

        [Fact]
        public void CreateStructuralProperty_ThrowsArgumentNull_StructuralProperty()
        {
            Assert.ThrowsArgumentNull(
                () => _serializer.CreateStructuralProperty(structuralProperty: null, entityInstance: 42, writeContext: null),
                "structuralProperty");
        }

        [Fact]
        public void CreateStructuralProperty_ThrowsArgumentNull_EntityInstance()
        {
            Mock<IEdmStructuralProperty> property = new Mock<IEdmStructuralProperty>();

            Assert.ThrowsArgumentNull(
                () => _serializer.CreateStructuralProperty(property.Object, entityInstance: null, writeContext: null),
                "entityInstance");
        }

        [Fact]
        public void CreateStructuralProperty_ThrowsSerializationException_TypeCannotBeSerialized()
        {
            // Arrange
            Mock<IEdmTypeReference> propertyType = new Mock<IEdmTypeReference>();
            propertyType.Setup(t => t.Definition).Returns(new EdmEntityType("Namespace", "Name"));
            Mock<IEdmStructuralProperty> property = new Mock<IEdmStructuralProperty>();
            Mock<ODataSerializerProvider> serializerProvider = new Mock<ODataSerializerProvider>(MockBehavior.Strict);
            object entity = new object();
            property.Setup(p => p.Type).Returns(propertyType.Object);
            serializerProvider.Setup(s => s.GetEdmTypeSerializer(propertyType.Object)).Returns<ODataEdmTypeSerializer>(null);

            var serializer = new ODataEntityTypeSerializer(_serializer.EntityType, serializerProvider.Object);

            // Act & Assert
            Assert.Throws<SerializationException>(
                () => serializer.CreateStructuralProperty(property.Object, entity, new ODataSerializerContext()),
                "'Namespace.Name' cannot be serialized using the ODataMediaTypeFormatter.");
        }

        [Fact]
        public void CreateStructuralProperty_Calls_CreateCreateODataValueOnInnerSerializer()
        {
            // Arrange
            Mock<IEdmTypeReference> propertyType = new Mock<IEdmTypeReference>();
            propertyType.Setup(t => t.Definition).Returns(new EdmEntityType("Namespace", "Name"));
            Mock<IEdmStructuralProperty> property = new Mock<IEdmStructuralProperty>();
            property.Setup(p => p.Name).Returns("PropertyName");
            Mock<ODataSerializerProvider> serializerProvider = new Mock<ODataSerializerProvider>(MockBehavior.Strict);
            object entity = new { PropertyName = 42 };
            Mock<ODataEdmTypeSerializer> innerSerializer = new Mock<ODataEdmTypeSerializer>(propertyType.Object, ODataPayloadKind.Property);
            ODataValue propertyValue = new Mock<ODataValue>().Object;

            property.Setup(p => p.Type).Returns(propertyType.Object);
            serializerProvider.Setup(s => s.GetEdmTypeSerializer(propertyType.Object)).Returns(innerSerializer.Object);
            innerSerializer.Setup(s => s.CreateODataValue(42, _writeContext)).Returns(propertyValue).Verifiable();

            var serializer = new ODataEntityTypeSerializer(_serializer.EntityType, serializerProvider.Object);

            // Act
            ODataProperty createdProperty = serializer.CreateStructuralProperty(property.Object, entity, _writeContext);

            // Assert
            innerSerializer.Verify();
            Assert.Equal("PropertyName", createdProperty.Name);
            Assert.Equal(propertyValue, createdProperty.Value);
        }

        private void VerifyEntityInstanceContext(EntityInstanceContext instanceContext, ODataSerializerContext writeContext)
        {
            Assert.Equal(writeContext.Model, instanceContext.EdmModel);
            Assert.Equal(writeContext.EntitySet, instanceContext.EntitySet);
            Assert.Equal(writeContext.Request, instanceContext.Request);
            Assert.Equal(writeContext.SkipExpensiveAvailabilityChecks, instanceContext.SkipExpensiveAvailabilityChecks);
            Assert.Equal(writeContext.Url, instanceContext.Url);
        }

        [Fact]
        public void CreateEntry_UsesCorrectTypeName()
        {
            EntityInstanceContext instanceContext = new EntityInstanceContext();
            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EdmType, new DefaultODataSerializerProvider());
            serializer.Setup(s => s.CreateStructuralPropertyBag(instanceContext, _writeContext)).Returns(Enumerable.Empty<ODataProperty>());
            serializer.Setup(s => s.CreateODataActions(instanceContext, _writeContext)).Returns(Enumerable.Empty<ODataAction>());
            serializer.CallBase = true;

            // Act
            ODataEntry entry = serializer.Object.CreateEntry(instanceContext, _writeContext);

            // Assert
            Assert.Equal("Default.Customer", entry.TypeName);
        }

        [Fact]
        public void CreateODataActions_ThrowsArgumentNull_EntityInstanceContext()
        {
            Assert.ThrowsArgumentNull(
                () => _serializer.CreateODataActions(entityInstanceContext: null, writeContext: null),
                "entityInstanceContext");
        }

        [Fact]
        public void CreateODataActions_ThrowsArgumentNull_WriteContext()
        {
            Assert.ThrowsArgumentNull(
                () => _serializer.CreateODataActions(new EntityInstanceContext(), writeContext: null),
                "writeContext");
        }

        [Fact]
        public void CreateODataActions_Calls_CreateODataActionForAllActionsOnEntityType()
        {
            // Arrange
            IEdmFunctionImport[] actions = new[] { new Mock<IEdmFunctionImport>().Object, new Mock<IEdmFunctionImport>().Object };
            ODataAction[] odataActions = new[] { new ODataAction(), new ODataAction() };
            _model.SetAnnotationValue<BindableProcedureFinder>(_model, new FakeBindableProcedureFinder(actions));
            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EntityType, new DefaultODataSerializerProvider());
            EntityInstanceContext entityInstanceContext = new EntityInstanceContext { EdmModel = _model, EntityType = _customerSet.ElementType };

            serializer.CallBase = true;
            serializer.Setup(s => s.CreateODataAction(actions[0], entityInstanceContext, It.IsAny<ODataMetadataLevel>())).Returns(odataActions[0]).Verifiable();
            serializer.Setup(s => s.CreateODataAction(actions[1], entityInstanceContext, It.IsAny<ODataMetadataLevel>())).Returns(odataActions[1]).Verifiable();

            // Act
            var result =
                serializer.Object.CreateODataActions(entityInstanceContext, new ODataSerializerContext { MetadataLevel = ODataMetadataLevel.Default }).ToArray();

            // Assert
            serializer.Verify();
            Assert.Equal(odataActions, result);
        }

        [Fact]
        public void CreateODataAction_ThrowsArgumentNull_Action()
        {
            Assert.ThrowsArgumentNull(
                () => _serializer.CreateODataAction(action: null, entityInstanceContext: null, metadataLevel: ODataMetadataLevel.Default),
                "action");
        }

        [Fact]
        public void CreateODataAction_ThrowsArgumentNull_EntityInstanceContext()
        {
            IEdmFunctionImport action = new Mock<IEdmFunctionImport>().Object;

            Assert.ThrowsArgumentNull(
                () => _serializer.CreateODataAction(action, entityInstanceContext: null, metadataLevel: ODataMetadataLevel.Default),
                "entityInstanceContext");
        }

        [Fact]
        public void CreateODataAction_ReturnsNull_IfModelDoesntHaveActionLinkBuilder()
        {
            Mock<IEdmFunctionImport> action = new Mock<IEdmFunctionImport>();
            Assert.Null(
                _serializer.CreateODataAction(action.Object, new EntityInstanceContext { EdmModel = EdmCoreModel.Instance }, ODataMetadataLevel.Default));
        }

        [Fact]
        public void CreateEntry_WritesCorrectIdLink()
        {
            // Arrange
            EntityInstanceContext instanceContext = new EntityInstanceContext();
            bool customIdLinkbuilderCalled = false;
            EntitySetLinkBuilderAnnotation linkAnnotation = new MockEntitySetLinkBuilderAnnotation
            {
                IdLinkBuilder = new SelfLinkBuilder<string>((EntityInstanceContext context) =>
                {
                    Assert.Same(instanceContext, context);
                    customIdLinkbuilderCalled = true;
                    return "http://sample_id_link";
                },
                followsConventions: false)
            };
            _model.SetEntitySetLinkBuilderAnnotation(_customerSet, linkAnnotation);

            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EdmType, new DefaultODataSerializerProvider());
            serializer.Setup(s => s.CreateStructuralPropertyBag(instanceContext, _writeContext)).Returns(Enumerable.Empty<ODataProperty>());
            serializer.Setup(s => s.CreateODataActions(instanceContext, _writeContext)).Returns(Enumerable.Empty<ODataAction>());
            serializer.CallBase = true;

            // Act
            ODataEntry entry = serializer.Object.CreateEntry(instanceContext, _writeContext);

            // Assert
            Assert.True(customIdLinkbuilderCalled);
        }

        [Fact]
        public void WriteObjectInline_WritesCorrectEditLink()
        {
            // Arrange
            EntityInstanceContext instanceContext = new EntityInstanceContext();
            bool customEditLinkbuilderCalled = false;
            EntitySetLinkBuilderAnnotation linkAnnotation = new MockEntitySetLinkBuilderAnnotation
            {
                EditLinkBuilder = new SelfLinkBuilder<Uri>((EntityInstanceContext context) =>
                {
                    Assert.Same(instanceContext, context);
                    customEditLinkbuilderCalled = true;
                    return new Uri("http://sample_edit_link");
                },
                followsConventions: false)
            };
            _model.SetEntitySetLinkBuilderAnnotation(_customerSet, linkAnnotation);

            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EdmType, new DefaultODataSerializerProvider());
            serializer.Setup(s => s.CreateStructuralPropertyBag(instanceContext, _writeContext)).Returns(Enumerable.Empty<ODataProperty>());
            serializer.Setup(s => s.CreateODataActions(instanceContext, _writeContext)).Returns(Enumerable.Empty<ODataAction>());
            serializer.CallBase = true;

            // Act
            ODataEntry entry = serializer.Object.CreateEntry(instanceContext, _writeContext);

            // Assert
            Assert.True(customEditLinkbuilderCalled);
        }

        [Fact]
        public void WriteObjectInline_WritesCorrectReadLink()
        {
            // Arrange
            EntityInstanceContext instanceContext = new EntityInstanceContext();
            bool customReadLinkbuilderCalled = false;
            EntitySetLinkBuilderAnnotation linkAnnotation = new MockEntitySetLinkBuilderAnnotation
            {
                ReadLinkBuilder = new SelfLinkBuilder<Uri>((EntityInstanceContext context) =>
                {
                    Assert.Same(instanceContext, context);
                    customReadLinkbuilderCalled = true;
                    return new Uri("http://sample_read_link");
                },
                followsConventions: false)
            };

            _model.SetEntitySetLinkBuilderAnnotation(_customerSet, linkAnnotation);

            Mock<ODataEntityTypeSerializer> serializer = new Mock<ODataEntityTypeSerializer>(_serializer.EdmType, new DefaultODataSerializerProvider());
            serializer.Setup(s => s.CreateStructuralPropertyBag(instanceContext, _writeContext)).Returns(Enumerable.Empty<ODataProperty>());
            serializer.Setup(s => s.CreateODataActions(instanceContext, _writeContext)).Returns(Enumerable.Empty<ODataAction>());
            serializer.CallBase = true;

            // Act
            ODataEntry entry = serializer.Object.CreateEntry(instanceContext, _writeContext);

            // Assert
            Assert.True(customReadLinkbuilderCalled);
        }

        [Fact]
        public void AddTypeNameAnnotationAsNeeded_DoesNotAddAnnotation_InDefaultMetadataMode()
        {
            // Arrange
            ODataEntry entry = new ODataEntry();

            // Act
            ODataEntityTypeSerializer.AddTypeNameAnnotationAsNeeded(entry, null, ODataMetadataLevel.Default);

            // Assert
            Assert.Null(entry.GetAnnotation<SerializationTypeNameAnnotation>());
        }

        [Fact]
        public void AddTypeNameAnnotationAsNeeded_AddsAnnotation_InJsonLightMetadataMode()
        {
            // Arrange
            string expectedTypeName = "TypeName";
            ODataEntry entry = new ODataEntry
            {
                TypeName = expectedTypeName
            };

            // Act
            ODataEntityTypeSerializer.AddTypeNameAnnotationAsNeeded(entry, null, ODataMetadataLevel.MinimalMetadata);

            // Assert
            SerializationTypeNameAnnotation annotation = entry.GetAnnotation<SerializationTypeNameAnnotation>();
            Assert.NotNull(annotation); // Guard
            Assert.Equal(expectedTypeName, annotation.TypeName);
        }

        [Theory]
        [InlineData(TestODataMetadataLevel.Default, false)]
        [InlineData(TestODataMetadataLevel.FullMetadata, true)]
        [InlineData(TestODataMetadataLevel.MinimalMetadata, true)]
        [InlineData(TestODataMetadataLevel.NoMetadata, true)]
        public void ShouldAddTypeNameAnnotation(TestODataMetadataLevel metadataLevel, bool expectedResult)
        {
            // Act
            bool actualResult = ODataEntityTypeSerializer.ShouldAddTypeNameAnnotation(
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.Equal(expectedResult, actualResult);
        }

        [Theory]
        [InlineData("MatchingType", "MatchingType", TestODataMetadataLevel.FullMetadata, false)]
        [InlineData("DoesNotMatch1", "DoesNotMatch2", TestODataMetadataLevel.FullMetadata, false)]
        [InlineData("MatchingType", "MatchingType", TestODataMetadataLevel.MinimalMetadata, true)]
        [InlineData("DoesNotMatch1", "DoesNotMatch2", TestODataMetadataLevel.MinimalMetadata, false)]
        [InlineData("MatchingType", "MatchingType", TestODataMetadataLevel.NoMetadata, true)]
        [InlineData("DoesNotMatch1", "DoesNotMatch2", TestODataMetadataLevel.NoMetadata, true)]
        public void ShouldSuppressTypeNameSerialization(string entryType, string entitySetType,
            TestODataMetadataLevel metadataLevel, bool expectedResult)
        {
            // Arrange
            ODataEntry entry = new ODataEntry
            {
                // The caller uses a namespace-qualified name, which this test leaves empty.
                TypeName = "." + entryType
            };
            IEdmEntitySet entitySet = CreateEntitySetWithElementTypeName(entitySetType);

            // Act
            bool actualResult = ODataEntityTypeSerializer.ShouldSuppressTypeNameSerialization(entry, entitySet,
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public void CreateODataAction_ForAtom_IncludesEverything()
        {
            // Arrange
            string expectedContainerName = "Container";
            string expectedActionName = "Action";
            string expectedTarget = "aa://Target";
            string expectedMetadataPrefix = "http://Metadata";

            IEdmEntityContainer container = CreateFakeContainer(expectedContainerName);
            IEdmFunctionImport functionImport = CreateFakeFunctionImport(container, expectedActionName,
                isBindable: true);

            ActionLinkBuilder linkBuilder = new ActionLinkBuilder((a) => new Uri(expectedTarget),
                followsConventions: true);
            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            annotationsManager.SetActionLinkBuilder(functionImport, linkBuilder);
            annotationsManager.SetIsAlwaysBindable(functionImport);
            annotationsManager.SetDefaultContainer(container);
            IEdmModel model = CreateFakeModel(annotationsManager);
            UrlHelper url = CreateMetadataLinkFactory(expectedMetadataPrefix);

            EntityInstanceContext context = CreateContext(model, url);

            // Act
            ODataAction actualAction = _serializer.CreateODataAction(functionImport, context,
                ODataMetadataLevel.Default);

            // Assert
            string expectedMetadata = expectedMetadataPrefix + "#" + expectedContainerName + "." + expectedActionName;
            ODataAction expectedAction = new ODataAction
            {
                Metadata = new Uri(expectedMetadata),
                Target = new Uri(expectedTarget),
                Title = expectedActionName
            };

            AssertEqual(expectedAction, actualAction);
        }

        [Fact]
        public void CreateODataAction_OmitsAction_WhenActionLinkBuilderReturnsNull()
        {
            // Arrange
            IEdmEntityContainer container = CreateFakeContainer("IgnoreContainer");
            IEdmFunctionImport functionImport = CreateFakeFunctionImport(container, "IgnoreAction");

            ActionLinkBuilder linkBuilder = new ActionLinkBuilder((a) => null, followsConventions: false);
            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            annotationsManager.SetActionLinkBuilder(functionImport, linkBuilder);

            IEdmModel model = CreateFakeModel(annotationsManager);

            EntityInstanceContext context = CreateContext(model);

            // Act
            ODataAction actualAction = _serializer.CreateODataAction(functionImport, context,
                ODataMetadataLevel.MinimalMetadata);

            // Assert
            Assert.Null(actualAction);
        }

        [Fact]
        public void CreateODataAction_ForJsonLight_OmitsContainerName_PerCreateMetadataFragment()
        {
            // Arrange
            string expectedMetadataPrefix = "http://Metadata";
            string expectedActionName = "Action";

            IEdmEntityContainer container = CreateFakeContainer("ContainerShouldNotAppearInResult");
            IEdmFunctionImport functionImport = CreateFakeFunctionImport(container, expectedActionName);

            ActionLinkBuilder linkBuilder = new ActionLinkBuilder((a) => new Uri("aa://IgnoreTarget"),
                followsConventions: false);
            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            annotationsManager.SetActionLinkBuilder(functionImport, linkBuilder);
            annotationsManager.SetDefaultContainer(container);

            IEdmModel model = CreateFakeModel(annotationsManager);
            UrlHelper url = CreateMetadataLinkFactory(expectedMetadataPrefix);

            EntityInstanceContext context = CreateContext(model, url);

            // Act
            ODataAction actualAction = _serializer.CreateODataAction(functionImport, context,
                ODataMetadataLevel.MinimalMetadata);

            // Assert
            Assert.NotNull(actualAction);
            string expectedMetadata = expectedMetadataPrefix + "#" + expectedActionName;
            AssertEqual(new Uri(expectedMetadata), actualAction.Metadata);
        }

        [Fact]
        public void CreateODataAction_SkipsAlwaysAvailableAction_PerShouldOmitAction()
        {
            // Arrange
            IEdmFunctionImport functionImport = CreateFakeFunctionImport(true);

            ActionLinkBuilder linkBuilder = new ActionLinkBuilder((a) => new Uri("aa://IgnoreTarget"),
                followsConventions: true);
            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            annotationsManager.SetActionLinkBuilder(functionImport, linkBuilder);
            annotationsManager.SetIsAlwaysBindable(functionImport);

            IEdmModel model = CreateFakeModel(annotationsManager);

            EntityInstanceContext context = CreateContext(model);

            // Act
            ODataAction actualAction = _serializer.CreateODataAction(functionImport, context,
                ODataMetadataLevel.MinimalMetadata);

            // Assert
            Assert.Null(actualAction);
        }

        [Theory]
        [InlineData(TestODataMetadataLevel.Default)]
        [InlineData(TestODataMetadataLevel.FullMetadata)]
        public void CreateODataAction_IncludesTitle(TestODataMetadataLevel metadataLevel)
        {
            // Arrange
            string expectedActionName = "Action";

            IEdmEntityContainer container = CreateFakeContainer("IgnoreContainer");
            IEdmFunctionImport functionImport = CreateFakeFunctionImport(container, expectedActionName);

            ActionLinkBuilder linkBuilder = new ActionLinkBuilder((a) => new Uri("aa://IgnoreTarget"),
                followsConventions: false);
            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            annotationsManager.SetActionLinkBuilder(functionImport, linkBuilder);

            IEdmModel model = CreateFakeModel(annotationsManager);
            UrlHelper url = CreateMetadataLinkFactory("http://IgnoreMetadataPath");

            EntityInstanceContext context = CreateContext(model, url);

            // Act
            ODataAction actualAction = _serializer.CreateODataAction(functionImport, context,
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.NotNull(actualAction);
            Assert.Equal(expectedActionName, actualAction.Title);
        }

        [Theory]
        [InlineData(TestODataMetadataLevel.MinimalMetadata)]
        [InlineData(TestODataMetadataLevel.NoMetadata)]
        public void CreateODataAction_OmitsTitle(TestODataMetadataLevel metadataLevel)
        {
            // Arrange
            IEdmEntityContainer container = CreateFakeContainer("IgnoreContainer");
            IEdmFunctionImport functionImport = CreateFakeFunctionImport(container, "IgnoreAction");

            ActionLinkBuilder linkBuilder = new ActionLinkBuilder((a) => new Uri("aa://Ignore"),
                followsConventions: false);
            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            annotationsManager.SetActionLinkBuilder(functionImport, linkBuilder);

            IEdmModel model = CreateFakeModel(annotationsManager);
            UrlHelper url = CreateMetadataLinkFactory("http://IgnoreMetadataPath");

            EntityInstanceContext context = CreateContext(model, url);

            // Act
            ODataAction actualAction = _serializer.CreateODataAction(functionImport, context,
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.NotNull(actualAction);
            Assert.Null(actualAction.Title);
        }

        [Theory]
        [InlineData(TestODataMetadataLevel.Default, false)]
        [InlineData(TestODataMetadataLevel.Default, true)]
        [InlineData(TestODataMetadataLevel.FullMetadata, false)]
        [InlineData(TestODataMetadataLevel.FullMetadata, true)]
        [InlineData(TestODataMetadataLevel.MinimalMetadata, false)]
        [InlineData(TestODataMetadataLevel.NoMetadata, false)]
        public void CreateODataAction_IncludesTarget(TestODataMetadataLevel metadataLevel, bool followsConventions)
        {
            // Arrange
            Uri expectedTarget = new Uri("aa://Target");

            IEdmEntityContainer container = CreateFakeContainer("IgnoreContainer");
            IEdmFunctionImport functionImport = CreateFakeFunctionImport(container, "IgnoreAction");

            ActionLinkBuilder linkBuilder = new ActionLinkBuilder((a) => expectedTarget, followsConventions);
            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            annotationsManager.SetActionLinkBuilder(functionImport, linkBuilder);

            IEdmModel model = CreateFakeModel(annotationsManager);
            UrlHelper url = CreateMetadataLinkFactory("http://IgnoreMetadataPath");

            EntityInstanceContext context = CreateContext(model, url);

            // Act
            ODataAction actualAction = _serializer.CreateODataAction(functionImport, context,
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.NotNull(actualAction);
            Assert.Equal(expectedTarget, actualAction.Target);
        }

        [Theory]
        [InlineData(TestODataMetadataLevel.MinimalMetadata)]
        [InlineData(TestODataMetadataLevel.NoMetadata)]
        public void CreateODataAction_OmitsTarget_WhenFollowingConventions(TestODataMetadataLevel metadataLevel)
        {
            // Arrange
            IEdmEntityContainer container = CreateFakeContainer("IgnoreContainer");
            IEdmFunctionImport functionImport = CreateFakeFunctionImport(container, "IgnoreAction");

            ActionLinkBuilder linkBuilder = new ActionLinkBuilder((a) => new Uri("aa://Ignore"),
                followsConventions: true);
            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            annotationsManager.SetActionLinkBuilder(functionImport, linkBuilder);

            IEdmModel model = CreateFakeModel(annotationsManager);
            UrlHelper url = CreateMetadataLinkFactory("http://IgnoreMetadataPath");

            EntityInstanceContext context = CreateContext(model, url);

            // Act
            ODataAction actualAction = _serializer.CreateODataAction(functionImport, context,
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.NotNull(actualAction);
            Assert.Null(actualAction.Target);
        }

        [InlineData(TestODataMetadataLevel.Default)]
        [InlineData(TestODataMetadataLevel.FullMetadata)]
        [InlineData(TestODataMetadataLevel.MinimalMetadata)]
        [InlineData(TestODataMetadataLevel.NoMetadata)]
        public void CreateMetadataFragment_IncludesNonDefaultContainerName(TestODataMetadataLevel metadataLevel)
        {
            // Arrange
            string expectedContainerName = "Container";
            string expectedActionName = "Action";

            IEdmEntityContainer container = CreateFakeContainer(expectedContainerName);
            IEdmFunctionImport action = CreateFakeFunctionImport(container, expectedActionName);

            IEdmModel model = CreateFakeModel();

            // Act
            string actualFragment = ODataEntityTypeSerializer.CreateMetadataFragment(action, model,
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.Equal(expectedContainerName + "." + expectedActionName, actualFragment);
        }

        [Theory]
        [InlineData(TestODataMetadataLevel.Default)]
        [InlineData(TestODataMetadataLevel.FullMetadata)]
        public void CreateMetadataFragment_IncludesDefaultContainerName(TestODataMetadataLevel metadataLevel)
        {
            // Arrange
            string expectedContainerName = "Container";
            string expectedActionName = "Action";

            IEdmEntityContainer container = CreateFakeContainer(expectedContainerName);
            IEdmFunctionImport action = CreateFakeFunctionImport(container, expectedActionName);

            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            annotationsManager.SetDefaultContainer(container);
            IEdmModel model = CreateFakeModel(annotationsManager);

            // Act
            string actualFragment = ODataEntityTypeSerializer.CreateMetadataFragment(action, model,
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.Equal(expectedContainerName + "." + expectedActionName, actualFragment);
        }

        [Theory]
        [InlineData(TestODataMetadataLevel.MinimalMetadata)]
        [InlineData(TestODataMetadataLevel.NoMetadata)]
        public void CreateMetadataFragment_OmitsDefaultContainerName(TestODataMetadataLevel metadataLevel)
        {
            // Arrange
            string expectedActionName = "Action";

            IEdmEntityContainer container = CreateFakeContainer("ContainerShouldNotAppearInResult");
            IEdmFunctionImport action = CreateFakeFunctionImport(container, expectedActionName);

            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            annotationsManager.SetDefaultContainer(container);
            IEdmModel model = CreateFakeModel(annotationsManager);

            // Act
            string actualFragment = ODataEntityTypeSerializer.CreateMetadataFragment(action, model,
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.Equal(expectedActionName, actualFragment);
        }

        [Theory]
        [InlineData(TestODataMetadataLevel.Default, false, false, false)]
        [InlineData(TestODataMetadataLevel.Default, false, true, false)]
        [InlineData(TestODataMetadataLevel.Default, true, false, false)]
        [InlineData(TestODataMetadataLevel.Default, true, true, false)]
        [InlineData(TestODataMetadataLevel.FullMetadata, false, false, false)]
        [InlineData(TestODataMetadataLevel.FullMetadata, false, true, false)]
        [InlineData(TestODataMetadataLevel.FullMetadata, true, false, false)]
        [InlineData(TestODataMetadataLevel.FullMetadata, true, true, false)]
        [InlineData(TestODataMetadataLevel.MinimalMetadata, false, false, false)]
        [InlineData(TestODataMetadataLevel.MinimalMetadata, false, true, false)]
        [InlineData(TestODataMetadataLevel.MinimalMetadata, true, false, false)]
        [InlineData(TestODataMetadataLevel.MinimalMetadata, true, true, true)]
        [InlineData(TestODataMetadataLevel.NoMetadata, false, false, false)]
        [InlineData(TestODataMetadataLevel.NoMetadata, false, true, false)]
        [InlineData(TestODataMetadataLevel.NoMetadata, true, false, false)]
        [InlineData(TestODataMetadataLevel.NoMetadata, true, true, true)]
        public void TestShouldOmitAction(TestODataMetadataLevel metadataLevel, bool isAlwaysAvailable,
            bool followsConventions, bool expectedResult)
        {
            // Arrange
            IEdmFunctionImport action = CreateFakeFunctionImport(true);
            IEdmDirectValueAnnotationsManager annonationsManager = CreateFakeAnnotationsManager();

            if (isAlwaysAvailable)
            {
                annonationsManager.SetIsAlwaysBindable(action);
            }

            IEdmModel model = CreateFakeModel(annonationsManager);

            ActionLinkBuilder builder = new ActionLinkBuilder((a) => { throw new NotImplementedException(); },
                followsConventions);

            // Act
            bool actualResult = ODataEntityTypeSerializer.ShouldOmitAction(action, model, builder,
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.Equal(expectedResult, actualResult);
        }

        private static IEdmNavigationProperty CreateFakeNavigationProperty(string name, IEdmTypeReference type)
        {
            Mock<IEdmNavigationProperty> property = new Mock<IEdmNavigationProperty>();
            property.Setup(p => p.Name).Returns(name);
            property.Setup(p => p.Type).Returns(type);
            return property.Object;
        }

        private static void AssertEqual(ODataAction expected, ODataAction actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);
                return;
            }

            Assert.NotNull(actual);
            AssertEqual(expected.Metadata, actual.Metadata);
            AssertEqual(expected.Target, actual.Target);
            Assert.Equal(expected.Title, actual.Title);
        }

        private static void AssertEqual(Uri expected, Uri actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);
                return;
            }

            Assert.NotNull(actual);
            Assert.Equal(expected.AbsoluteUri, actual.AbsoluteUri);
        }

        private static EntityInstanceContext CreateContext(IEdmModel model)
        {
            return new EntityInstanceContext
            {
                EdmModel = model
            };
        }

        private static EntityInstanceContext CreateContext(IEdmModel model, UrlHelper url)
        {
            return new EntityInstanceContext
            {
                EdmModel = model,
                Url = url,
            };
        }

        private static IEdmEntitySet CreateEntitySetWithElementTypeName(string typeName)
        {
            Mock<IEdmEntityType> entityTypeMock = new Mock<IEdmEntityType>();
            entityTypeMock.Setup(o => o.Name).Returns(typeName);
            IEdmEntityType entityType = entityTypeMock.Object;
            Mock<IEdmEntitySet> entitySetMock = new Mock<IEdmEntitySet>();
            entitySetMock.Setup(o => o.ElementType).Returns(entityType);
            return entitySetMock.Object;
        }

        private static IEdmDirectValueAnnotationsManager CreateFakeAnnotationsManager()
        {
            return new FakeAnnotationsManager();
        }

        private static IEdmEntityContainer CreateFakeContainer(string name)
        {
            Mock<IEdmEntityContainer> mock = new Mock<IEdmEntityContainer>();
            mock.Setup(o => o.Name).Returns(name);
            return mock.Object;
        }

        private static IEdmFunctionImport CreateFakeFunctionImport(IEdmEntityContainer container, string name)
        {
            Mock<IEdmFunctionImport> mock = new Mock<IEdmFunctionImport>();
            mock.Setup(o => o.Container).Returns(container);
            mock.Setup(o => o.Name).Returns(name);
            return mock.Object;
        }

        private static IEdmFunctionImport CreateFakeFunctionImport(IEdmEntityContainer container, string name,
            bool isBindable)
        {
            Mock<IEdmFunctionImport> mock = new Mock<IEdmFunctionImport>();
            mock.Setup(o => o.Container).Returns(container);
            mock.Setup(o => o.Name).Returns(name);
            mock.Setup(o => o.IsBindable).Returns(isBindable);
            return mock.Object;
        }

        private static IEdmFunctionImport CreateFakeFunctionImport(bool isBindable)
        {
            Mock<IEdmFunctionImport> mock = new Mock<IEdmFunctionImport>();
            mock.Setup(o => o.IsBindable).Returns(isBindable);
            return mock.Object;
        }

        private static IEdmModel CreateFakeModel()
        {
            IEdmDirectValueAnnotationsManager annotationsManager = CreateFakeAnnotationsManager();
            return CreateFakeModel(annotationsManager);
        }

        private static IEdmModel CreateFakeModel(IEdmDirectValueAnnotationsManager annotationsManager)
        {
            Mock<IEdmModel> model = new Mock<IEdmModel>();
            model.Setup(m => m.DirectValueAnnotationsManager).Returns(annotationsManager);
            return model.Object;
        }

        private static UrlHelper CreateMetadataLinkFactory(string metadataPath)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, metadataPath);
            HttpConfiguration configuration = new HttpConfiguration();
            configuration.Routes.MapFakeODataRoute();
            request.SetConfiguration(configuration);
            request.SetFakeODataRouteName();
            return new UrlHelper(request);
        }

        private class Customer
        {
            public Customer()
            {
                this.Orders = new List<Order>();
            }
            public int ID { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public IList<Order> Orders { get; private set; }
        }

        private class Order
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public Customer Customer { get; set; }
        }

        private class FakeBindableProcedureFinder : BindableProcedureFinder
        {
            private IEdmFunctionImport[] _procedures;

            public FakeBindableProcedureFinder(params IEdmFunctionImport[] procedures)
                : base(EdmCoreModel.Instance)
            {
                _procedures = procedures;
            }

            public override IEnumerable<IEdmFunctionImport> FindProcedures(IEdmEntityType entityType)
            {
                return _procedures;
            }
        }

        private class FakeAnnotationsManager : IEdmDirectValueAnnotationsManager
        {
            IDictionary<Tuple<IEdmElement, string, string>, object> annotations =
                new Dictionary<Tuple<IEdmElement, string, string>, object>();

            public object GetAnnotationValue(IEdmElement element, string namespaceName, string localName)
            {
                object value;

                if (!annotations.TryGetValue(CreateKey(element, namespaceName, localName), out value))
                {
                    return null;
                }

                return value;
            }

            public object[] GetAnnotationValues(IEnumerable<IEdmDirectValueAnnotationBinding> annotations)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<IEdmDirectValueAnnotation> GetDirectValueAnnotations(IEdmElement element)
            {
                throw new NotImplementedException();
            }

            public void SetAnnotationValue(IEdmElement element, string namespaceName, string localName, object value)
            {
                annotations[CreateKey(element, namespaceName, localName)] = value;
            }

            public void SetAnnotationValues(IEnumerable<IEdmDirectValueAnnotationBinding> annotations)
            {
                throw new NotImplementedException();
            }

            private static Tuple<IEdmElement, string, string> CreateKey(IEdmElement element, string namespaceName,
                string localName)
            {
                return new Tuple<IEdmElement, string, string>(element, namespaceName, localName);
            }
        }

    }
}
