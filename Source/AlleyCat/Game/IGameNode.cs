using AlleyCat.Autowire;
using AlleyCat.Common;
using AlleyCat.Event;

namespace AlleyCat.Game
{
    [NonInjectable]
    public interface IGameNode : ILifecycleAware, IValidatable
    {
    }
}
