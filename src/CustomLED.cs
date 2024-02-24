using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;

namespace OTD.CustomLED;

public class CustomLED : ITool
{
    #region Initialization

    public bool Initialize()
    {
        _ = Task.Run(InitializeCore);

        return true;
    }

    private Task InitializeCore()
    {
        
    }

    #endregion

    #region Properties



    #endregion

    #region Disposal

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    #endregion
}
