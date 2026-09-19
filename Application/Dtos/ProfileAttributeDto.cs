using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Dtos;

public sealed record ProfileAttributeDto(AttributeValueDto Value, DetailedAttributeDto Attribute);

