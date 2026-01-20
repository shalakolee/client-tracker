using Microsoft.Maui.ApplicationModel;

namespace ClientTracker;

public static class ShellFactory
{
    private static bool _routesRegistered;

    public static Shell CreateShell()
    {
        RegisterRoutesOnce();

        return DeviceInfo.Platform == DevicePlatform.Android
            ? new MobileShell()
            : new AppShell();
    }

    private static void RegisterRoutesOnce()
    {
        if (_routesRegistered)
        {
            return;
        }

        _routesRegistered = true;
        Routing.RegisterRoute("client-view", typeof(Pages.ClientViewPage));
        Routing.RegisterRoute("client-edit", typeof(Pages.ClientEditPage));
        Routing.RegisterRoute("sale-details", typeof(Pages.SaleDetailsPage));
        Routing.RegisterRoute("sale-add", typeof(Pages.AddSalePage));
        Routing.RegisterRoute("contact-details", typeof(Pages.ContactDetailsPage));
        Routing.RegisterRoute("contact-edit", typeof(Pages.ContactEditPage));
        Routing.RegisterRoute("commission-plans", typeof(Pages.CommissionPlansPage));
        Routing.RegisterRoute("commission-plan-edit", typeof(Pages.CommissionPlanEditPage));
    }
}
