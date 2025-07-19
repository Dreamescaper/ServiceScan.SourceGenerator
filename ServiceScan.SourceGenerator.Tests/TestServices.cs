namespace External;

public interface IExternalService;
public class ExternalService1 : IExternalService { }
public class ExternalService2 : IExternalService { }

// Shouldn't be added as type is not accessible from other assembly
internal class InternalExternalService2 : IExternalService { }