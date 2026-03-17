using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class LettersUI : MonoBehaviour
    {
        public event Action<int> OnBadgeCountChanged;

        private VisualElement inboxView;
        private VisualElement friendsView;
        private VisualElement addView;
        private VisualElement giftPickerView;

        private Button tabInbox;
        private Button tabFriends;
        private Button tabAdd;

        private ScrollView inboxList;
        private ScrollView friendsList;
        private Label inboxEmpty;
        private Label friendsEmpty;

        private TextField displayNameInput;
        private Label displayNameStatus;
        private Label myFriendCode;
        private TextField friendCodeInput;
        private Button sendRequestBtn;
        private Button shareCodeBtn;
        private Label shareStatus;
        private Label addFriendStatus;

        // Tab text labels (child Labels inside each Button, separate from button.text)
        private Label tabInboxLabel;
        private Label tabFriendsLabel;
        private Label tabAddLabel;

        // Badges
        private Label badgeInbox;
        private Label badgeFriends;
        private Label badgeAdd;
        private int inboxCount;
        private int friendsCount;
        private int pendingRequestCount;

        // Mobile keyboard handling
        private TouchScreenKeyboard activeKeyboard;
        private TextField activeKeyboardTarget;

        // Gift picker
        private Label giftPickerTo;
        private ScrollView giftPickerInventory;
        private Label giftSelectedLabel;
        private Button confirmGiftBtn;
        private Button cancelGiftBtn;

        private VisualTreeAsset friendTemplate;
        private VisualTreeAsset giftTemplate;
        private VisualTreeAsset requestTemplate;
        private VisualTreeAsset giftPickerItemTemplate;

        private string giftTargetUid;
        private string giftTargetName;
        private readonly List<GiftItem> selectedGiftItems = new();

        // Card element pools — reused across refreshes to avoid DOM churn
        // that causes ScrollView to clamp scrollOffset and visibly jump.
        private readonly List<VisualElement> inboxCardPool = new();
        private readonly List<VisualElement> friendsCardPool = new();
        private readonly List<VisualElement> giftPickerCardPool = new();

        public void Initialize(VisualElement root)
        {
            // Tab buttons
            tabInbox = root.Q<Button>("tab-inbox");
            tabFriends = root.Q<Button>("tab-friends");
            tabAdd = root.Q<Button>("tab-add");

            // Views
            inboxView = root.Q("letters-inbox");
            friendsView = root.Q("letters-friends");
            addView = root.Q("letters-add");
            giftPickerView = root.Q("letters-gift-picker");

            // Inbox
            inboxList = root.Q<ScrollView>("inbox-list");
            inboxEmpty = root.Q<Label>("inbox-empty");

            // Friends
            friendsList = root.Q<ScrollView>("friends-list");
            friendsEmpty = root.Q<Label>("friends-empty");

            // Display name
            displayNameInput = root.Q<TextField>("display-name-input");
            displayNameStatus = root.Q<Label>("display-name-status");

            // Add friend
            myFriendCode = root.Q<Label>("my-friend-code");
            friendCodeInput = root.Q<TextField>("friend-code-input");
            sendRequestBtn = root.Q<Button>("btn-send-request");
            shareCodeBtn = root.Q<Button>("btn-share-code");
            shareStatus = root.Q<Label>("share-status");
            addFriendStatus = root.Q<Label>("add-friend-status");

            // Gift picker
            giftPickerTo = root.Q<Label>("gift-picker-to");
            giftPickerInventory = root.Q<ScrollView>("gift-picker-inventory");
            giftSelectedLabel = root.Q<Label>("gift-selected-label");
            confirmGiftBtn = root.Q<Button>("btn-confirm-gift");
            cancelGiftBtn = root.Q<Button>("btn-cancel-gift");

            // Tab text labels — first Label child that isn't the badge
            tabInboxLabel = GetTabTextLabel(tabInbox);
            tabFriendsLabel = GetTabTextLabel(tabFriends);
            tabAddLabel = GetTabTextLabel(tabAdd);

            // Badges
            badgeInbox = root.Q<Label>("badge-inbox");
            badgeFriends = root.Q<Label>("badge-friends");
            badgeAdd = root.Q<Label>("badge-add");

            // Templates
            friendTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/FriendItem");
            giftTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/GiftItem");
            requestTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/FriendRequestItem");
            giftPickerItemTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/GiftPickerItem");

            // Wire tabs
            tabInbox?.RegisterCallback<ClickEvent>(_ => ShowTab("inbox"));
            tabFriends?.RegisterCallback<ClickEvent>(_ => ShowTab("friends"));
            tabAdd?.RegisterCallback<ClickEvent>(_ => ShowTab("add"));

            RefreshTabLabels();
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged += RefreshTabLabels;

            // Wire display name and friend code input
            if (Application.isMobilePlatform)
            {
                // On mobile, make TextFields read-only and open TouchScreenKeyboard on tap
                // This avoids UI Toolkit's buggy double-keyboard behavior on iOS
                SetupMobileTextField(displayNameInput, TouchScreenKeyboardType.Default);
                SetupMobileTextField(friendCodeInput, TouchScreenKeyboardType.Default);
            }
            else
            {
                // Desktop: use normal TextField behavior
                displayNameInput?.RegisterCallback<FocusOutEvent>(_ => OnDisplayNameSubmit());
                displayNameInput?.Q("unity-text-input")?.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                        OnDisplayNameSubmit();
                });
            }

            // Auto-capitalize and auto-dash friend code input (format: PREFIX-XXXX)
            friendCodeInput?.RegisterValueChangedCallback(evt =>
            {
                var val = evt.newValue?.ToUpperInvariant() ?? "";
                var prev = evt.previousValue ?? "";

                // Backspacing the dash also removes the last prefix char
                if (prev.Length == 6 && prev[5] == '-' && val.Length == 5)
                    val = val.Substring(0, 4);

                // Auto-insert dash after 5-char prefix
                if (val.Length == 5 && !val.Contains("-") && prev.Length < val.Length)
                    val += "-";

                if (val != evt.newValue)
                    friendCodeInput.SetValueWithoutNotify(val);
            });

            // Wire add friend button
            sendRequestBtn?.RegisterCallback<ClickEvent>(_ => OnSendFriendRequest());
            shareCodeBtn?.RegisterCallback<ClickEvent>(_ => OnShareCode());

            // Wire gift picker
            confirmGiftBtn?.RegisterCallback<ClickEvent>(_ => OnConfirmGift());
            cancelGiftBtn?.RegisterCallback<ClickEvent>(_ => OnCancelGift());

            // Default to inbox
            ShowTab("inbox");

            // Subscribe to social events
            if (SocialService.Instance != null)
            {
                SocialService.Instance.OnSignedIn += OnSignedIn;
                SocialService.Instance.OnFriendListUpdated += OnFriendListUpdated;
            }
        }

        private static Label GetTabTextLabel(Button tab)
        {
            if (tab == null) return null;
            foreach (var child in tab.Children())
            {
                if (child is Label label && !label.ClassListContains("tab-badge"))
                    return label;
            }
            return null;
        }

        private void RefreshTabLabels()
        {
            if (tabInboxLabel != null) tabInboxLabel.text = Loc.Get("ui.letters.inbox", "Inbox");
            if (tabFriendsLabel != null) tabFriendsLabel.text = Loc.Get("ui.letters.friends", "Friends");
            if (tabAddLabel != null) tabAddLabel.text = Loc.Get("ui.letters.add", "Add");
        }

        private void OnDestroy()
        {
            if (SocialService.Instance != null)
            {
                SocialService.Instance.OnSignedIn -= OnSignedIn;
                SocialService.Instance.OnFriendListUpdated -= OnFriendListUpdated;
            }
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged -= RefreshTabLabels;
        }

        private void ShowTab(string tab)
        {
            inboxView.style.display = tab == "inbox" ? DisplayStyle.Flex : DisplayStyle.None;
            friendsView.style.display = tab == "friends" ? DisplayStyle.Flex : DisplayStyle.None;
            addView.style.display = tab == "add" ? DisplayStyle.Flex : DisplayStyle.None;
            giftPickerView.style.display = DisplayStyle.None;

            tabInbox?.RemoveFromClassList("letters-tab--active");
            tabFriends?.RemoveFromClassList("letters-tab--active");
            tabAdd?.RemoveFromClassList("letters-tab--active");

            switch (tab)
            {
                case "inbox": tabInbox?.AddToClassList("letters-tab--active"); RefreshInbox(); break;
                case "friends": tabFriends?.AddToClassList("letters-tab--active"); RefreshFriends(); break;
                case "add": tabAdd?.AddToClassList("letters-tab--active"); RefreshAddView(); break;
            }
        }

        private void OnSignedIn()
        {
            RefreshAddView();
            RefreshInbox();
            RefreshFriends();
        }

        private static void UpdateTabBadge(Label badge, int count)
        {
            if (badge == null) return;
            if (count > 0)
            {
                badge.text = count.ToString();
                badge.style.display = DisplayStyle.Flex;
            }
            else
            {
                badge.style.display = DisplayStyle.None;
            }
        }

        private void EmitBadgeCount()
        {
            int total = inboxCount + pendingRequestCount;
            OnBadgeCountChanged?.Invoke(total);
        }

        // ── Card pool helper ──

        /// <summary>
        /// Swaps the children of a ScrollView without ever dropping content height to zero.
        /// New children are added first, then old children are removed. This prevents
        /// the ScrollView from clamping scrollOffset and causing a visible jump.
        /// </summary>
        private static void SwapChildren(ScrollView list, List<VisualElement> pool, List<VisualElement> newChildren)
        {
            if (list == null) return;

            // Add new children first (height grows or stays same)
            foreach (var child in newChildren)
                list.Add(child);

            // Remove old children (height normalizes to new content)
            foreach (var old in pool)
                old.RemoveFromHierarchy();

            // Update pool
            pool.Clear();
            pool.AddRange(newChildren);
        }

        // ── Inbox ──

        private async void RefreshInbox()
        {
            if (SocialService.Instance == null || !SocialService.Instance.IsSignedIn)
            {
                SwapChildren(inboxList, inboxCardPool, new List<VisualElement>());
                if (inboxEmpty != null)
                {
                    inboxEmpty.text = Loc.Get("ui.letters.connect_error", "Could not connect to server");
                    inboxEmpty.style.display = DisplayStyle.Flex;
                }
                return;
            }

            var newChildren = new List<VisualElement>();

            var requests = await SocialService.Instance.GetPendingRequests();
            foreach (var req in requests)
            {
                if (requestTemplate == null) break;
                var el = requestTemplate.CloneTree();
                var nameLabel = el.Q<Label>(className: "request-from");
                if (nameLabel != null) nameLabel.text = string.Format(Loc.Get("ui.letters.wants_friends", "{0} wants to be friends"), req.fromName);

                var acceptBtn = el.Q<Button>(className: "btn-accept");
                var declineBtn = el.Q<Button>(className: "btn-decline");
                string reqId = req.id;

                acceptBtn?.RegisterCallback<ClickEvent>(async _ =>
                {
                    await SocialService.Instance.AcceptFriendRequest(reqId);
                    RefreshInbox();
                });
                declineBtn?.RegisterCallback<ClickEvent>(async _ =>
                {
                    await SocialService.Instance.DeclineFriendRequest(reqId);
                    RefreshInbox();
                });

                newChildren.Add(el);
            }

            var gifts = await SocialService.Instance.GetPendingGifts();
            foreach (var gift in gifts)
            {
                if (giftTemplate == null) break;
                var el = giftTemplate.CloneTree();
                var fromLabel = el.Q<Label>(className: "gift-from");
                if (fromLabel != null) fromLabel.text = $"From {gift.fromName}";

                var contentsLabel = el.Q<Label>(className: "gift-contents");
                if (contentsLabel != null)
                {
                    var parts = new List<string>();
                    foreach (var item in gift.items)
                        parts.Add($"{item.name} x{item.count}");
                    contentsLabel.text = string.Join(", ", parts);
                }

                var claimBtn = el.Q<Button>(className: "btn-claim-gift");
                string giftId = gift.id;
                var giftItems = gift.items;

                claimBtn?.RegisterCallback<ClickEvent>(async _ =>
                {
                    await SocialService.Instance.ClaimGift(giftId, giftItems);
                    RefreshInbox();
                });

                newChildren.Add(el);
            }

            SwapChildren(inboxList, inboxCardPool, newChildren);

            bool isEmpty = requests.Count == 0 && gifts.Count == 0;
            if (inboxEmpty != null)
            {
                inboxEmpty.text = Loc.Get("ui.letters.no_letters", "No new letters");
                inboxEmpty.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            }

            inboxCount = gifts.Count;
            pendingRequestCount = requests.Count;
            UpdateTabBadge(badgeInbox, gifts.Count + requests.Count);
            EmitBadgeCount();
        }

        // ── Friends ──

        private async void RefreshFriends()
        {
            if (SocialService.Instance == null || !SocialService.Instance.IsSignedIn)
            {
                SwapChildren(friendsList, friendsCardPool, new List<VisualElement>());
                if (friendsEmpty != null)
                {
                    friendsEmpty.text = Loc.Get("ui.letters.connect_error", "Could not connect to server");
                    friendsEmpty.style.display = DisplayStyle.Flex;
                }
                return;
            }

            // Fetch latest from server so we see newly accepted friends
            await SocialService.Instance.RefreshFriendList();
            // UI is rebuilt by OnFriendListUpdated event handler
        }

        private void OnFriendListUpdated(List<CachedFriend> friends)
        {
            RebuildFriendsList(friends);
        }

        private void RebuildFriendsList(List<CachedFriend> friends)
        {
            friendsCount = friends?.Count ?? 0;
            UpdateTabBadge(badgeFriends, friendsCount);

            if (friends == null || friends.Count == 0)
            {
                SwapChildren(friendsList, friendsCardPool, new List<VisualElement>());
                if (friendsEmpty != null)
                {
                    friendsEmpty.text = Loc.Get("ui.letters.no_friends", "No friends yet");
                    friendsEmpty.style.display = DisplayStyle.Flex;
                }
                return;
            }
            if (friendsEmpty != null) friendsEmpty.style.display = DisplayStyle.None;

            var newChildren = new List<VisualElement>();

            foreach (var friend in friends)
            {
                if (friendTemplate == null) break;
                var el = friendTemplate.CloneTree();

                var nameLabel = el.Q<Label>(className: "friend-name");
                if (nameLabel != null) nameLabel.text = friend.displayName;

                var codeLabel = el.Q<Label>(className: "friend-code-small");
                if (codeLabel != null) codeLabel.text = friend.friendCode;

                var visitBtn = el.Q<Button>(className: "btn-visit");
                var giftBtn = el.Q<Button>(className: "btn-send-gift");
                string friendUid = friend.uid;
                string friendName = friend.displayName;

                var deleteBtn = el.Q<Button>(className: "btn-delete-friend");

                visitBtn?.RegisterCallback<ClickEvent>(_ => OnVisitFriend(friendUid, friendName));
                giftBtn?.RegisterCallback<ClickEvent>(_ => OnOpenGiftPicker(friendUid, friendName));
                deleteBtn?.RegisterCallback<ClickEvent>(_ => OnDeleteFriend(friendUid, friendName));

                newChildren.Add(el);
            }

            SwapChildren(friendsList, friendsCardPool, newChildren);
        }

        // ── Add Friend ──

        private void RefreshAddView()
        {
            if (SocialService.Instance == null || !SocialService.Instance.IsSignedIn)
            {
                if (displayNameStatus != null) displayNameStatus.text = Loc.Get("ui.letters.connect_error", "Could not connect to server");
                if (myFriendCode != null) myFriendCode.text = "";
                if (addFriendStatus != null) addFriendStatus.text = "";
                return;
            }

            if (displayNameInput != null)
            {
                var currentName = SocialSaveManager.Instance?.Data?.displayName ?? "Camper";
                displayNameInput.SetValueWithoutNotify(currentName);
            }
            if (displayNameStatus != null) displayNameStatus.text = "";

            if (myFriendCode != null)
            {
                var code = SocialService.Instance?.FriendCode;
                myFriendCode.text = string.IsNullOrEmpty(code) ? Loc.Get("ui.letters.code_loading", "Your code: loading...") : string.Format(Loc.Get("ui.letters.your_code", "Your code: {0}"), code);
            }
            if (addFriendStatus != null) addFriendStatus.text = "";
        }

        private void OnShareCode()
        {
            var code = SocialService.Instance?.FriendCode;
            if (string.IsNullOrEmpty(code))
            {
                if (shareStatus != null) shareStatus.text = Loc.Get("ui.letters.code_unavailable", "Code not available yet");
                return;
            }

            var name = SocialSaveManager.Instance?.Data?.displayName ?? "A friend";
            string message = $"{name} wants to be your friend in Camp Fire!\ncampfire://invite/{code}";

            GUIUtility.systemCopyBuffer = message;
            if (shareStatus != null) shareStatus.text = Loc.Get("ui.letters.copied", "Copied to clipboard!");
        }

        private async void OnDisplayNameSubmit()
        {
            if (displayNameInput == null || SocialService.Instance == null) return;

            var newName = displayNameInput.value?.Trim();
            var currentName = SocialSaveManager.Instance?.Data?.displayName ?? "";

            // No change — skip
            if (newName == currentName) return;

            if (!SocialService.Instance.IsSignedIn)
            {
                if (displayNameStatus != null) displayNameStatus.text = Loc.Get("ui.letters.not_signed_in", "Not signed in");
                return;
            }

            if (!SocialService.IsValidDisplayName(newName))
            {
                if (displayNameStatus != null) displayNameStatus.text = Loc.Get("ui.letters.name_format", "Letters, numbers, spaces only (1-20 chars)");
                displayNameInput.SetValueWithoutNotify(currentName);
                return;
            }

            if (displayNameStatus != null) displayNameStatus.text = Loc.Get("ui.letters.saving", "Saving...");

            bool success = await SocialService.Instance.UpdateDisplayName(newName);
            if (displayNameStatus != null)
                displayNameStatus.text = success ? Loc.Get("ui.letters.name_saved", "Name saved!") : Loc.Get("ui.letters.name_failed", "Failed to save name");

            if (!success)
                displayNameInput.SetValueWithoutNotify(currentName);
        }

        private async void OnSendFriendRequest()
        {
            var code = friendCodeInput?.value?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                if (addFriendStatus != null) addFriendStatus.text = Loc.Get("ui.letters.enter_code", "Enter a friend code");
                return;
            }

            if (addFriendStatus != null) addFriendStatus.text = Loc.Get("ui.letters.sending", "Sending...");

            bool success = await SocialService.Instance.SendFriendRequest(code);
            if (addFriendStatus != null)
                addFriendStatus.text = success ? Loc.Get("ui.letters.request_sent", "Request sent!") : Loc.Get("ui.letters.code_not_found", "Code not found or already friends");

            if (success && friendCodeInput != null)
                friendCodeInput.value = "";
        }

        // ── Visit ──

        private async void OnVisitFriend(string friendUid, string friendName)
        {
            var snapshot = await SocialService.Instance.FetchVillageSnapshot(friendUid);
            if (snapshot == null)
            {
                Debug.Log("LettersUI: No village snapshot available for this friend");
                return;
            }

            CampFireUI.Instance?.CloseOverlay();
            var campsiteView = GetComponent<CampsiteViewUI>();
            campsiteView?.EnterVisitMode(snapshot, friendName);
        }

        private async void OnDeleteFriend(string friendUid, string friendName)
        {
            bool success = await SocialService.Instance.RemoveFriend(friendUid);
            if (success)
            {
                Debug.Log($"LettersUI: Removed friend {friendName}");
                RefreshFriends();
            }
            else
            {
                Debug.LogWarning($"LettersUI: Failed to remove friend {friendName}");
            }
        }

        // ── Gift Picker ──

        private void OnOpenGiftPicker(string friendUid, string friendName)
        {
            giftTargetUid = friendUid;
            giftTargetName = friendName;
            selectedGiftItems.Clear();

            // Hide other views, show picker
            inboxView.style.display = DisplayStyle.None;
            friendsView.style.display = DisplayStyle.None;
            addView.style.display = DisplayStyle.None;
            giftPickerView.style.display = DisplayStyle.Flex;

            if (giftPickerTo != null) giftPickerTo.text = $"To: {friendName}";
            UpdateGiftSelectedLabel();
            PopulateGiftInventory();
        }

        private void PopulateGiftInventory()
        {
            var newChildren = new List<VisualElement>();
            var data = SaveManager.Instance.Data;

            foreach (var item in data.inventory)
            {
                if (item.count <= 0) continue;
                var itemConfig = ConfigService.Instance?.GetItem(item.itemKey);
                string displayName = ConfigService.Instance?.GetItemDisplayName(item.itemKey) ?? item.itemKey;
                var el = MakeGiftPickerRow(itemConfig?.category == "seed" ? "seed" : "item",
                    item.itemKey, item.count, displayName);
                if (el != null) newChildren.Add(el);
            }

            SwapChildren(giftPickerInventory, giftPickerCardPool, newChildren);
        }

        private VisualElement MakeGiftPickerRow(string type, string name, int available, string displayName = null)
        {
            if (giftPickerItemTemplate == null) return null;
            var el = giftPickerItemTemplate.CloneTree();

            var nameLabel = el.Q<Label>(className: "picker-item-name");
            if (nameLabel != null) nameLabel.text = displayName ?? name;

            var countLabel = el.Q<Label>(className: "picker-item-count");
            if (countLabel != null) countLabel.text = $"x{available}";

            var addBtn = el.Q<Button>(className: "btn-add-to-gift");
            addBtn?.RegisterCallback<ClickEvent>(_ =>
            {
                if (selectedGiftItems.Count >= 3) return;

                var existing = selectedGiftItems.Find(g => g.name == name && g.type == type);
                int alreadySelected = existing?.count ?? 0;
                if (alreadySelected >= available) return;

                if (existing != null)
                    existing.count++;
                else
                    selectedGiftItems.Add(new GiftItem { type = type, name = name, count = 1 });

                UpdateGiftSelectedLabel();
            });

            return el;
        }

        private void UpdateGiftSelectedLabel()
        {
            if (giftSelectedLabel == null) return;
            if (selectedGiftItems.Count == 0)
            {
                giftSelectedLabel.text = Loc.Get("ui.letters.selected_none", "Selected: none");
                return;
            }

            var parts = new List<string>();
            foreach (var item in selectedGiftItems)
                parts.Add($"{item.name} x{item.count}");
            giftSelectedLabel.text = $"Selected: {string.Join(", ", parts)}";
        }

        private async void OnConfirmGift()
        {
            if (selectedGiftItems.Count == 0 || string.IsNullOrEmpty(giftTargetUid)) return;

            bool success = await SocialService.Instance.SendGift(giftTargetUid, selectedGiftItems);
            if (success)
            {
                selectedGiftItems.Clear();
                ShowTab("friends");
            }
        }

        private void OnCancelGift()
        {
            selectedGiftItems.Clear();
            giftTargetUid = null;
            ShowTab("friends");
        }

        // ── Mobile Keyboard ──

        private void SetupMobileTextField(TextField field, TouchScreenKeyboardType keyboardType)
        {
            if (field == null) return;
            field.isReadOnly = true;
            field.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopPropagation();
                OpenMobileKeyboard(field, keyboardType);
            });
        }

        private void OpenMobileKeyboard(TextField field, TouchScreenKeyboardType keyboardType)
        {
            if (activeKeyboard != null && activeKeyboard.status == TouchScreenKeyboard.Status.Visible)
                return;

            int maxLength = field == displayNameInput ? 20 : 0;
            activeKeyboardTarget = field;
            activeKeyboard = TouchScreenKeyboard.Open(
                field.value ?? "",
                keyboardType,
                false, // autocorrection
                false, // multiline
                false, // secure
                false, // alert
                "",    // placeholder
                maxLength
            );
        }

        private void Update()
        {
            if (activeKeyboard == null || activeKeyboardTarget == null) return;

            // Update text field live as user types
            if (activeKeyboard.status == TouchScreenKeyboard.Status.Visible)
            {
                activeKeyboardTarget.SetValueWithoutNotify(activeKeyboard.text);
            }

            if (activeKeyboard.status != TouchScreenKeyboard.Status.Visible)
            {
                if (activeKeyboard.status == TouchScreenKeyboard.Status.Done)
                {
                    activeKeyboardTarget.SetValueWithoutNotify(activeKeyboard.text);

                    if (activeKeyboardTarget == displayNameInput)
                        OnDisplayNameSubmit();
                    else if (activeKeyboardTarget == friendCodeInput)
                        OnSendFriendRequest();
                }

                activeKeyboard = null;
                activeKeyboardTarget = null;
            }
        }
    }
}
