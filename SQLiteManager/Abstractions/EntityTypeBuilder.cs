using System.Linq.Expressions;
using System.Reflection;

namespace SQLiteManager.Abstractions;

public class EntityTypeBuilder<T>(ModelBuilder modelBuilder)
{
    private readonly ModelBuilder _modelBuilder = modelBuilder;
    private readonly EntityMetadata _entityMeta = modelBuilder.GetOrAddEntity(typeof(T));

    // Define the key property
    public EntityTypeBuilder<T> HasKey<TProp>(Expression<Func<T, TProp>> keyExpression)
    {
        if (keyExpression.Body is MemberExpression memberExpr)
        {
            _entityMeta.KeyProperty = (PropertyInfo)memberExpr.Member;
        }
        else if (keyExpression.Body is UnaryExpression unaryExpr
                 && unaryExpr.Operand is MemberExpression operandExpr)
        {
            _entityMeta.KeyProperty = (PropertyInfo)operandExpr.Member;
        }
        return this;
    }

    // Define a one-to-many: Principal T has many TChild
    public RelationshipBuilder<T, TChild> HasMany<TChild>(Expression<Func<T, IEnumerable<TChild>>> navigation)
    {
        // Get navigation property name on principal
        var memberExpr = (MemberExpression)navigation.Body;
        string principalNav = memberExpr.Member.Name;

        var rel = new Relationship
        {
            PrincipalType = typeof(T),
            PrincipalNavigation = principalNav,
            DependentType = typeof(TChild)
        };
        _modelBuilder.AddRelationship(rel);
        return new RelationshipBuilder<T, TChild>(_modelBuilder, rel);
    }

    // Define a many-to-one: Dependent T has one TChild (principal)
    public RelationshipBuilder<TChild, T> HasOne<TChild>(Expression<Func<T, TChild>> navigation)
    {
        // Get navigation property name on dependent
        var memberExpr = (MemberExpression)navigation.Body;
        string dependentNav = memberExpr.Member.Name;

        var rel = new Relationship
        {
            DependentType = typeof(T),
            DependentNavigation = dependentNav,
            PrincipalType = typeof(TChild)
        };
        _modelBuilder.AddRelationship(rel);
        return new RelationshipBuilder<TChild, T>(_modelBuilder, rel);
    }
}

public class RelationshipBuilder<TPrincipal, TDependent>(ModelBuilder modelBuilder, Relationship relationship)
{
    private readonly ModelBuilder _modelBuilder = modelBuilder;
    private readonly Relationship _relationship = relationship;

    // For HasMany(...).WithOne(...)
    public RelationshipBuilder<TPrincipal, TDependent> WithOne(Expression<Func<TDependent, TPrincipal>> navigation)
    {
        var memberExpr = (MemberExpression)navigation.Body;
        _relationship.DependentNavigation = memberExpr.Member.Name;
        return this;
    }

    // For HasOne(...).WithMany(...)
    public RelationshipBuilder<TPrincipal, TDependent> WithMany(Expression<Func<TPrincipal, IEnumerable<TDependent>>> navigation)
    {
        var memberExpr = (MemberExpression)navigation.Body;
        _relationship.PrincipalNavigation = memberExpr.Member.Name;
        return this;
    }

    // Specify the foreign key property on the dependent type
    public RelationshipBuilder<TPrincipal, TDependent> HasForeignKey<TForeignKey>(Expression<Func<TDependent, TForeignKey>> foreignKeyExpression)
    {
        if (foreignKeyExpression.Body is MemberExpression memberExpr)
        {
            _relationship.ForeignKeyProperty = (PropertyInfo)memberExpr.Member;
        }
        else if (foreignKeyExpression.Body is UnaryExpression unaryExpr
                 && unaryExpr.Operand is MemberExpression operandExpr)
        {
            _relationship.ForeignKeyProperty = (PropertyInfo)operandExpr.Member;
        }
        return this;
    }
}

