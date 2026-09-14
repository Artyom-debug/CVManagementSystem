using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Domain.Enums;

namespace Application.Dtos;

public sealed record DetailedAttributeDto(
    Guid Id,
    int Version,
    string Name,
    string Description,
    AttributeType Type,
    Category Category,
    bool IsSystem,
    IReadOnlyList<AttributeOptionDto> Options);

public sealed record AttributeOptionDto(Guid Id, string Value);
