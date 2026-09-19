using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Domain.Enums;

namespace Application.Dtos;

public sealed record AttributeDto(Guid Id, int Version, string Name, AttributeType Type, Category Category, bool IsSystem);
