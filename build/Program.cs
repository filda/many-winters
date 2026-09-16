using ManyWinters.Build;
using Cake.Frosting;

return new CakeHost()
    .UseContext<BuildContext>()
    .Run(args);
