namespace DBXSync {

    public class TransferProgressReport {
        public int progress;
        public double bytesPerSecondSpeed;
        public string bytesPerSecondFormatted;

        public TransferProgressReport(int progress, double bytesPerSecond){
            this.progress = progress;
            this.bytesPerSecondSpeed = bytesPerSecond;
            this.bytesPerSecondFormatted = TransferSpeedTracker.FormatBytesPerSecond(bytesPerSecond);
        }
    }
}