using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Core.Infrastructure
{
    public interface IEntityMapper<TDomain, TEntity>
    {
        TDomain ToDomain(TEntity entity);
        TEntity ToEntity(TDomain domain);
    }
}