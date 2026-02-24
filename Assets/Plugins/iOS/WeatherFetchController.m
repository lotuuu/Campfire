#import "UnityAppController.h"
@import UserNotifications;
@import Foundation;

// NSUserDefaults keys — must match PlayerPrefs keys set from C#.
static NSString* const kApiKey    = @"weather_api_key";
static NSString* const kLatKey    = @"weather_lat";
static NSString* const kLonKey    = @"weather_lon";
static NSString* const kCondKey   = @"weather_condition";

@implementation UnityAppController (WeatherFetch)

- (void)application:(UIApplication*)application
    performFetchWithCompletionHandler:(void (^)(UIBackgroundFetchResult))completionHandler
{
    NSUserDefaults* defaults = [NSUserDefaults standardUserDefaults];
    NSString* apiKey  = [defaults stringForKey:kApiKey];
    double lat        = [defaults doubleForKey:kLatKey];
    double lon        = [defaults doubleForKey:kLonKey];
    NSInteger lastCond = [defaults integerForKey:kCondKey];

    if (!apiKey.length) {
        completionHandler(UIBackgroundFetchResultNoData);
        return;
    }

    NSString* urlStr = [NSString stringWithFormat:
        @"https://api.openweathermap.org/data/2.5/weather"
        @"?lat=%f&lon=%f&appid=%@&units=metric", lat, lon, apiKey];
    NSURL* url = [NSURL URLWithString:urlStr];

    NSURLSessionDataTask* task = [[NSURLSession sharedSession]
        dataTaskWithURL:url
      completionHandler:^(NSData* data, NSURLResponse* response, NSError* error) {

        if (error || !data) {
            completionHandler(UIBackgroundFetchResultFailed);
            return;
        }

        NSError* jsonErr;
        NSDictionary* json = [NSJSONSerialization JSONObjectWithData:data
                                                            options:0
                                                              error:&jsonErr];
        if (jsonErr || !json) {
            completionHandler(UIBackgroundFetchResultFailed);
            return;
        }

        NSArray* weatherArr = json[@"weather"];
        if (!weatherArr.count) {
            completionHandler(UIBackgroundFetchResultFailed);
            return;
        }

        NSInteger weatherId = [weatherArr[0][@"id"] integerValue];
        NSInteger newCond   = [self wf_mapCondition:weatherId];

        if (newCond == lastCond) {
            completionHandler(UIBackgroundFetchResultNoData);
            return;
        }

        [defaults setInteger:newCond forKey:kCondKey];
        [defaults synchronize];

        [self wf_scheduleNotificationForCondition:newCond];
        completionHandler(UIBackgroundFetchResultNewData);
    }];

    [task resume];
}

/// Maps OWM weather ID → WeatherCondition int. Must match C# MapCondition().
- (NSInteger)wf_mapCondition:(NSInteger)wid
{
    if (wid >= 200 && wid < 300) return 3; // Storm
    if (wid >= 300 && wid < 600) return 2; // Rain
    if (wid >= 600 && wid < 700) return 4; // Snow
    if (wid >= 801)              return 1; // Cloudy
    return 0;                              // Clear
}

- (NSString*)wf_messageForCondition:(NSInteger)cond
{
    switch (cond) {
        case 1:  return @"Clouds have rolled in over your garden";
        case 2:  return @"Rain has arrived \u2014 your plants are drinking deep";
        case 3:  return @"A storm is brewing over your garden";
        case 4:  return @"Snow is falling on your garden";
        default: return @"Clear skies over your garden";
    }
}

- (void)wf_scheduleNotificationForCondition:(NSInteger)cond
{
    UNUserNotificationCenter* center =
        [UNUserNotificationCenter currentNotificationCenter];

    UNMutableNotificationContent* content =
        [[UNMutableNotificationContent alloc] init];
    content.title = @"\U0001F33F Weather Update";
    content.body  = [self wf_messageForCondition:cond];
    content.sound = [UNNotificationSound defaultSound];

    // Fire after 1 second (minimum allowed interval for local notifications).
    UNTimeIntervalNotificationTrigger* trigger =
        [UNTimeIntervalNotificationTrigger triggerWithTimeInterval:1 repeats:NO];

    UNNotificationRequest* request =
        [UNNotificationRequest requestWithIdentifier:@"weather_change"
                                             content:content
                                             trigger:trigger];

    [center addNotificationRequest:request withCompletionHandler:nil];
}

@end
