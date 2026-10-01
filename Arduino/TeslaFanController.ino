// ============================================================================
// Project: NVIDIA Tesla Smart Fan Controller (Arduino Firmware)
// Co-created with: Google Gemini (AI Assistant)
// ============================================================================

// 25 kHz PWM configuration, Failsafe protection, and RPM calculator
volatile unsigned int pulseCount = 0;
unsigned long lastRPMCalc = 0;
unsigned long lastDataReceived = 0;
unsigned int currentRPM = 0;

const unsigned long FAILSAFE_TIMEOUT = 6000; // 6 seconds watchdog timeout

void rpmPulseISR() {
  pulseCount++;
}

void setup() {
  Serial.begin(9600); // Initialize serial communication
  
  pinMode(9, OUTPUT);
  pinMode(2, INPUT_PULLUP); // Tachometer pin with internal pull-up
  attachInterrupt(digitalPinToInterrupt(2), rpmPulseISR, FALLING);
  
  // Configure Timer 1 for true 25 kHz PWM frequency
  TCCR1A = _BV(COM1A1) | _BV(WGM11);
  TCCR1B = _BV(WGM13) | _BV(CS10);
  ICR1 = 320; // Top counter limit: 16MHz / (2 * 25000Hz) = 320
  
  setPWM(100); // Start at 100% speed for safety
  lastDataReceived = millis();
}

void loop() {
  // Watchdog check (Failsafe protection)
  if (millis() - lastDataReceived > FAILSAFE_TIMEOUT) {
    setPWM(100); // Spin fan at maximum speed if PC is not responding
  }

  // Read incoming bytes from the COM port
  if (Serial.available() > 0) {
    String input = Serial.readStringUntil('\n'); 
    int targetPwm = input.toInt(); // The PC sends calculated PWM percentage directly
    
    // Ensure the received value is a valid PWM percentage (0 to 100%)
    if (targetPwm >= 0 && targetPwm <= 100) {
      lastDataReceived = millis(); // Reset safety watchdog timer
      setPWM(targetPwm);           // Apply duty cycle to the fan directly
    }
  }

  // Calculate and transmit Fan RPM to Windows every 1 second
  if (millis() - lastRPMCalc >= 1000) {
    detachInterrupt(digitalPinToInterrupt(2)); // Temporarily disable interrupts for accuracy
    
    // Most standard fans output 2 pulses per single full revolution
    currentRPM = (pulseCount * 60) / 2; 
    pulseCount = 0;
    lastRPMCalc = millis();
    
    attachInterrupt(digitalPinToInterrupt(2), rpmPulseISR, FALLING);
    
    // Send RPM stats back to the PC application
    Serial.print("RPM:");
    Serial.println(currentRPM);
  }
}

// Function to update the PWM Duty Cycle (0 - 100%)
void setPWM(int percent) {
  int dutyCycle = map(percent, 0, 100, 0, 320);
  OCR1A = dutyCycle;
}
