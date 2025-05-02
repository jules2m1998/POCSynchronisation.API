using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteManager.Abstractions;

public interface IEntityTypeConfiguration<T>
{
    void Configure(EntityTypeBuilder<T> builder);
}