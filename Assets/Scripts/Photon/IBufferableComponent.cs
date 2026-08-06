public interface IBufferableComponent
{
    void BindBufferedObject(BufferedObject bufferedObject);
    void OnBufferedWake(int wakeTick, bool isActivationTick);
    void OnBufferedSleep(int sleepTick);
}
