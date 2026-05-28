using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BusinessLayer.DTOs.Category;

public class CategoryOrderUpdateDto
{
    public Guid Id { get; set; }
    public int OrderIndex { get; set; }
}
