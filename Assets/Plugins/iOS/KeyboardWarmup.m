#import <UIKit/UIKit.h>

// Pre-warms the iOS keyboard infrastructure by briefly making a hidden
// UITextField the first responder and immediately resigning it.
// The resign happens before the keyboard animation begins, so the
// keyboard never visually appears. This eliminates the ~5-10s lag
// on the first real keyboard open.

void _WarmUpKeyboard(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        UIWindow *window = UIApplication.sharedApplication.keyWindow;
        if (!window) return;

        UITextField *field = [[UITextField alloc] initWithFrame:CGRectZero];
        field.hidden = YES;
        [window addSubview:field];
        [field becomeFirstResponder];
        [field resignFirstResponder];
        [field removeFromSuperview];
    });
}
