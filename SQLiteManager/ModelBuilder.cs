using System.Reflection;

namespace SQLiteManager;

public class EntityMetadata
{
    public Type EntityType { get; set; } = null!;
    public PropertyInfo KeyProperty { get; set; } = null!;
}

public class Relationship
{
    public Type PrincipalType { get; set; } = null!;
    public string PrincipalNavigation { get; set; } = null!;
    public Type DependentType { get; set; } = null!;       // e.g. Post
    public string DependentNavigation { get; set; } = null!; // e.g. "User"
    public PropertyInfo ForeignKeyProperty { get; set; } = null!; // e.g. Post.UserId property
}

public class Model(Dictionary<Type, EntityMetadata> entities, List<Relationship> relationships)
{
    public Dictionary<Type, EntityMetadata> EntityTypes { get; } = entities;
    public List<Relationship> Relationships { get; } = relationships;
}

public class ModelBuilder
{
    private readonly Dictionary<Type, EntityMetadata> _entities = [];
    private readonly List<Relationship> _relationships = [];

    internal EntityMetadata GetOrAddEntity(Type type)
    {
        if (!_entities.TryGetValue(type, out var meta))
        {
            meta = new EntityMetadata { EntityType = type };
            _entities[type] = meta;
        }
        return meta;
    }
    // Add a relationship to the model
    internal void AddRelationship(Relationship relationship)
    {
        // Ensure both principal and dependent types are registered
        GetOrAddEntity(relationship.PrincipalType);
        GetOrAddEntity(relationship.DependentType);
        _relationships.Add(relationship);
    }
    // Finalize and produce the immutable Model
    public Model Build()
    {
        // Ensure every entity has a key; default to property "Id" if not explicitly set
        foreach (var meta in _entities.Values)
        {
            if (meta.KeyProperty == null)
            {
                var idProp = meta.EntityType.GetProperty("Id") ?? throw new Exception($"No key defined for {meta.EntityType.Name}");
                meta.KeyProperty = idProp;
            }
        }
        return new Model(new Dictionary<Type, EntityMetadata>(_entities), [.. _relationships]);
    }
}

