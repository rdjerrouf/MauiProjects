using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Media.Interfaces
{
    public interface IPhotoCompressionService
    {
        Task<Stream> CompressPhotoAsync(Stream originalPhotoStream, string fileName);
    }
}
