# Camp Fire

A campsite management game built around a magical flame (Spark of Ara). Built with Unity 6 (6000.3.6f1), 2D URP.

## Setup

### Secrets

The game uses the OpenWeatherMap API for real-world weather data. Create a secrets file at:

```
Assets/Resources/Config/secrets.json
```

With the following contents:

```json
{
  "openWeatherMapApiKey": "YOUR_API_KEY_HERE"
}
```

This file is gitignored. Without it, the weather system will not function on device (the Editor uses simulated weather by default).
