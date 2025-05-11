using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Location.Models
{
    public class GeoIndexedItem
    {
        public string? ItemId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
