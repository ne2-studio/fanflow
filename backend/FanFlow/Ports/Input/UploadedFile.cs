namespace FanFlow.Ports.Input;

public record UploadedFile(string ContentType, byte[] Content);
