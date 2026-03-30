namespace IngameScript
{
    /// <summary>
    /// A simple PID controller class.
    /// </summary>
    public class PIDController
    {
        private double kp;
        private double ki;
        private double kd;

        private double integral;
        private double previousError;

        public PIDController(double kp, double ki, double kd)
        {
            this.kp = kp;
            this.ki = ki;
            this.kd = kd;
            this.integral = 0;
            this.previousError = 0;
        }

        public double Update(double setpoint, double actual, double deltaTime)
        {
            double error = setpoint - actual;
            integral += error * deltaTime;
            double derivative = (error - previousError) / deltaTime;
            previousError = error;

            return kp * error + ki * integral + kd * derivative;
        }
    }
}