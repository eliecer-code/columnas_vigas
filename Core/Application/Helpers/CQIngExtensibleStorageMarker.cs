using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

/// <summary>
/// Provee métodos para marcar y leer elementos generados por el Addin utilizando Extensible Storage.
/// </summary>
public static class CQIngExtensibleStorageMarker
{
    // GUID único generado para este esquema. No debe cambiar nunca.
    private static readonly Guid SchemaGuid = new Guid("A1B2C3D4-E5F6-4A12-B8C9-123456789ABC");
    private const string SchemaName = "CQIng_GeneratedElement";
    private const string FieldParentWallId = "ParentWallId";
    private const string FieldElementType = "ElementType";

    private static Schema GetOrCreateSchema()
    {
        Schema schema = Schema.Lookup(SchemaGuid);
        if (schema != null) return schema;

        SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Vendor);
        builder.SetVendorId("CQIng");
        builder.SetSchemaName(SchemaName);

        // Campos
        builder.AddSimpleField(FieldParentWallId, typeof(ElementId));
        builder.AddSimpleField(FieldElementType, typeof(string));

        return builder.Finish();
    }

    /// <summary>
    /// Marca un elemento (FamilyInstance) indicando qué muro lo originó y qué tipo de elemento es.
    /// </summary>
    public static void MarkElement(Element element, ElementId parentWallId, string elementType)
    {
        if (element == null || parentWallId == ElementId.InvalidElementId) return;

        Schema schema = GetOrCreateSchema();
        Entity entity = new Entity(schema);
        
        entity.Set(FieldParentWallId, parentWallId);
        entity.Set(FieldElementType, elementType);

        element.SetEntity(entity);
    }

    /// <summary>
    /// Intenta leer los datos de marcado de un elemento.
    /// </summary>
    /// <returns>True si el elemento fue generado por el Addin, devolviendo su ParentWallId y Type.</returns>
    public static bool TryGetMarkerData(Element element, out ElementId parentWallId, out string elementType)
    {
        parentWallId = ElementId.InvalidElementId;
        elementType = string.Empty;

        if (element == null) return false;

        Schema schema = Schema.Lookup(SchemaGuid);
        if (schema == null) return false; // El esquema aún no ha sido creado en este documento

        Entity entity = element.GetEntity(schema);
        if (entity == null || !entity.IsValid()) return false;

        parentWallId = entity.Get<ElementId>(FieldParentWallId);
        elementType = entity.Get<string>(FieldElementType);

        return true;
    }
}
