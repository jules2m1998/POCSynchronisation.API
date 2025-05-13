using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Dapper.Abstractions;

public interface IPermissionManger
{
    Task<bool> CheckAndRequestStoragePermission();
}
