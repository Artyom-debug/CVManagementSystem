using Domain.Value_Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Dtos;

public sealed record ProjectDto(Guid Id, string Name, string Description, Period Period, IReadOnlyList<string> Tags);
