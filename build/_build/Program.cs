using Build.Context;
using Cake.Frosting;

return new CakeHost()
    .UseContext<BuildContext>() // Context can be added later
    .Run(args);
