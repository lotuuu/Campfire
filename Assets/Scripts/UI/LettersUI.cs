using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class LettersUI : MonoBehaviour
    {
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
        private Label addFriendStatus;

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
            addFriendStatus = root.Q<Label>("add-friend-status");

            // Gift picker
            giftPickerTo = root.Q<Label>("gift-picker-to");
            giftPickerInventory = root.Q<ScrollView>("gift-picker-inventory");
            giftSelectedLabel = root.Q<Label>("gift-selected-label");
            confirmGiftBtn = root.Q<Button>("btn-confirm-gift");
            cancelGiftBtn = root.Q<Button>("btn-cancel-gift");

            // Templates
            friendTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/FriendItem");
            giftTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/GiftItem");
            requestTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/FriendRequestItem");
            giftPickerItemTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/GiftPickerItem");

            // Wire tabs
            tabInbox?.RegisterCallback<ClickEvent>(_ => ShowTab("inbox"));
            tabFriends?.RegisterCallback<ClickEvent>(_ => ShowTab("friends"));
            tabAdd?.RegisterCallback<ClickEvent>(_ => ShowTab("add"));

            // Wire display name (submit on Enter / focus out)
            displayNameInput?.RegisterCallback<FocusOutEvent>(_ => OnDisplayNameSubmit());
            displayNameInput?.Q("unity-text-input")?.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    OnDisplayNameSubmit();
            });

            // Wire add friend button
            sendRequestBtn?.RegisterCallback<ClickEvent>(_ => OnSendFriendRequest());

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

        // ── Inbox ──

        private async void RefreshInbox()
        {
            if (SocialService.Instance == null || !SocialService.Instance.IsSignedIn)
            {
                inboxList?.Clear();
                if (inboxEmpty != null)
                {
                    inboxEmpty.text = "Could not connect to server";
                    inboxEmpty.style.display = DisplayStyle.Flex;
                }
                return;
            }

            inboxList?.Clear();

            var requests = await SocialService.Instance.GetPendingRequests();
            foreach (var req in requests)
            {
                if (requestTemplate == null) break;
                var el = requestTemplate.CloneTree();
                var nameLabel = el.Q<Label>(className: "request-from");
                if (nameLabel != null) nameLabel.text = $"{req.fromName} wants to be friends";

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

                inboxList?.Add(el);
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

                inboxList?.Add(el);
            }

            bool isEmpty = requests.Count == 0 && gifts.Count == 0;
            if (inboxEmpty != null)
            {
                inboxEmpty.text = "No new letters";
                inboxEmpty.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ── Friends ──

        private void RefreshFriends()
        {
            friendsList?.Clear();

            if (SocialService.Instance == null || !SocialService.Instance.IsSignedIn)
            {
                if (friendsEmpty != null)
                {
                    friendsEmpty.text = "Could not connect to server";
                    friendsEmpty.style.display = DisplayStyle.Flex;
                }
                return;
            }

            var friends = SocialSaveManager.Instance?.Data?.cachedFriends;
            if (friends == null || friends.Count == 0)
            {
                if (friendsEmpty != null)
                {
                    friendsEmpty.text = "No friends yet";
                    friendsEmpty.style.display = DisplayStyle.Flex;
                }
                return;
            }
            if (friendsEmpty != null) friendsEmpty.style.display = DisplayStyle.None;

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

                visitBtn?.RegisterCallback<ClickEvent>(_ => OnVisitFriend(friendUid, friendName));
                giftBtn?.RegisterCallback<ClickEvent>(_ => OnOpenGiftPicker(friendUid, friendName));

                friendsList?.Add(el);
            }
        }

        private void OnFriendListUpdated(List<CachedFriend> friends)
        {
            RefreshFriends();
        }

        // ── Add Friend ──

        private void RefreshAddView()
        {
            if (SocialService.Instance == null || !SocialService.Instance.IsSignedIn)
            {
                if (displayNameStatus != null) displayNameStatus.text = "Could not connect to server";
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
                myFriendCode.text = string.IsNullOrEmpty(code) ? "Your code: loading..." : $"Your code: {code}";
            }
            if (addFriendStatus != null) addFriendStatus.text = "";
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
                if (displayNameStatus != null) displayNameStatus.text = "Not signed in";
                return;
            }

            if (!SocialService.IsValidDisplayName(newName))
            {
                if (displayNameStatus != null) displayNameStatus.text = "Letters, numbers, spaces only (1-20 chars)";
                displayNameInput.SetValueWithoutNotify(currentName);
                return;
            }

            if (displayNameStatus != null) displayNameStatus.text = "Saving...";

            bool success = await SocialService.Instance.UpdateDisplayName(newName);
            if (displayNameStatus != null)
                displayNameStatus.text = success ? "Name saved!" : "Failed to save name";

            if (!success)
                displayNameInput.SetValueWithoutNotify(currentName);
        }

        private async void OnSendFriendRequest()
        {
            var code = friendCodeInput?.value?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                if (addFriendStatus != null) addFriendStatus.text = "Enter a friend code";
                return;
            }

            if (addFriendStatus != null) addFriendStatus.text = "Sending...";

            bool success = await SocialService.Instance.SendFriendRequest(code);
            if (addFriendStatus != null)
                addFriendStatus.text = success ? "Request sent!" : "Code not found or already friends";

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
            giftPickerInventory?.Clear();
            var data = SaveManager.Instance.Data;

            // Seeds
            foreach (var seed in data.seedInventory)
            {
                if (seed.count <= 0) continue;
                AddGiftPickerRow("seed", seed.seedName, seed.count);
            }

            // Items
            foreach (var item in data.items)
            {
                if (item.count <= 0) continue;
                AddGiftPickerRow("item", item.itemName, item.count);
            }
        }

        private void AddGiftPickerRow(string type, string name, int available)
        {
            if (giftPickerItemTemplate == null) return;
            var el = giftPickerItemTemplate.CloneTree();

            var nameLabel = el.Q<Label>(className: "picker-item-name");
            if (nameLabel != null) nameLabel.text = name;

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

            giftPickerInventory?.Add(el);
        }

        private void UpdateGiftSelectedLabel()
        {
            if (giftSelectedLabel == null) return;
            if (selectedGiftItems.Count == 0)
            {
                giftSelectedLabel.text = "Selected: none";
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
    }
}
