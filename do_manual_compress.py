import json

with open('JsonData/map_text.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

# Change MaxSize back to the true physical limit
data[0]['Config']['MaxSize'] = 3992

short_texts = [
    # 0
    "Iji: Don duel Jab![NL]U'll lose![3C][3D]Hon: None of ur biz![NL]Move![3C][3D]Iji: No![3C][3D]Hon: So mean![3C][3F]",
    # 1
    "Taru: Use Sunao![NL]...Sunao hit Iji![3C][3D]Iji: Hon, win![NL]I'm cheering![3C][3D]Hon: Wow! Mean Iji is[NL]honest![3C][3D]Taru: Yeah![33][3F]",
    # 2
    "Taru: Ah, Magic![NL]It stops time![NL]Hon: We did it![33][3F]",
    # 3
    "Hon: What's this?[NL]Taru: Sunao.[NL]Makes ppl honest![33][3F]",
    # 4
    "Jab: Late, Hon![NL]Hon: I'll win![NL]Jab: Who's the kid?[3C][3F]",
    # 5
    "Taru: I'm Taru! I[NL]will beat u for Hon![3C][3D]Hon: Count on u![33][3F]",
    # 6
    "Hon: Caught Jab![NL]Taru: We got him![NL]Jab: Damn...[33][3F]",
    # 7
    "Hara: As Water Champ,[NL]u can't win! Haha![NL]Hon: Damn...[3C][3F]",
    # 8
    "Taru: Use Suisui![NL]Hon: Thx![3C][3D]...Equipped Suisui![3C][3D]Hon: Caught him![NL]Taru: U did it![3C][3D]Hara: Impossible![33][3F]",
    # 9
    "Hon: Look! Touch![NL]Iji: Ur amazing![3C][3D]Taru: Hon is great![33][3F]",
    # 10
    "Taru: Caught Suisui![NL]Hon: Taru! Wrong target![33][3F]",
    # 11
    "Hon: Magic item![NL]Taru: Inryo![3C][3D]Now we collect Tako![33][3F]",
    # 12
    "Hon: Eek! Kabbo![NL]Taru: Who r u?[3C][3D]Kotarou: I'm Kotarou.[3C][3D]Here's 30 Tako.[3C][3D]Taru: Yay![33][3F]",
    # 13
    "Hon: Doburuku![3C][3D]Doburuku: I'm a hero![3C][3D]Hon: Beat Dowah![NL]Doburuku: Scary![3C][3D]Hon: We'll do it![3C][3D]Taru: Yeah![33][3F]",
    # 14
    "Erand: I'm Erand,[NL]Dowah's King![3C][3D]Prepare![NL]Hon: Go Taru![33][3F]",
    # 15
    "Hon: I'll win fast![NL]Rai: I'm Rakyu.[3C][3D]Go, Bat![33][3F]",
    # 16
    "Herm: U'll beat Dowah?[NL]I'll build a bridge![3C][3D]Go! Taru: Thx![33][3F]",
    # 17
    "Boch: I'll bite u![3C][3D]Hon: Taru! Careful![33][3F]",
    # 18
    "Gabu: I'll burn u![3C][3D]Taru: Yay! Warm![3C][3D]Hon: Don't be happy![33][3F]",
    # 19
    "Dowah: I'll execute u![3C][3D]Hon: Taru! Help![33][3F]",
    # 20
    "Taru: It's Bungaku![NL]Hon: We can go home![33][3F]",
    # 21
    "Taru: Amagumo![NL]He puts out fires![3C][3D]Hon: Convenient![33][3F]",
    # 22
    "Hara: I'm Ski Champ![3C][3D]Iyona, I'll win![3C][3D]Hara: I'll give Jab a[NL]handicap.[3C][3D]Jab: Don't mock me![3C][3D]Iji: Start![33][3F]",
    # 23
    "Taru: Kikun! We can[NL]attack enemies![33][3F]",
    # 24
    "Taru: Yay! Goal in![33][3F]",
    # 25
    "Hon: Inryo?[3C][3D]Taru: No. Sekiryo.[3C][3D]It repels bullets![33][3F]",
    # 26
    "Mimo: Taru, don't[NL]lose! I'm w/ u![3C][3F]",
    # 27
    "Taru: Ur dumb! So[NL]clingy! Go home![3C][3D]Hon: Taru, wait...[3C][3D]...Taru wore Zena![NL]Mimo: Taru...[3C][3F]",
    # 28
    "Taru: Zena makes u[NL]say bad things![3C][3D]Hon: Devilish item![33][3F]",
    # 29
    "Raibaa: Taru! Mimo[NL]is mine! I'll win.[NL]Hon: Taru! Don't lose![33][3F]",
    # 30
    "Hon: Terepe! We can[NL]return![3C][3D]Taru: Wow...Beep![33][3F]",
    # 31
    "Too cruel![33][3F]",
    # 32
    "Taru: Kankan![NL]It calms angry ppl.[NL]Hon: Useful![33][3F]",
    # 33
    "Wase: Why am I alone[NL]here?![3C][3D]Hon: He's angry...[33][3F]",
    # 34
    "Taru: Hon, use kettle![3C][3D]...Placed kettle![3C][3D]Wase: Oh, Hon. Check![33][3F]",
    # 35
    "Hon: A fan![3C][3D]Taru: Kaze-kun.[3C][3D]Blows enemies away![33][3F]",
    # 36
    "Hon: Ghost!![NL]Taru: Yay! Ghost![33][3F]",
    # 37
    "Jii: Welcome.[NL]All u can eat Tako.[3C][3D]Taru: Yay![NL]Hon: Wait for me![33][3F]",
    # 38
    "Hon: I'll shock Hara![NL]...Hon drank Horumon![3C][3D]Taru: Hon is a girl![3C][3D][31]Marue: Hello, I'm[NL]Marue. Let me pass?[3C][3D]Hara: Oh, sure.[33][3F]",
    # 39
    "Hara: U fool! I won't[NL]give Iyona to u![33][3F]",
    # 40
    "Taru: Bunshin! It[NL]clones u![3C][3D]Hon: Convenient![33][3F]",
    # 41
    "Hon: Oh, juice.[NL]Taru: No. Horumon.[3C][3D]U'll turn into a girl.[33][3F]",
    # 42
    "Hara: U tricked me![3C][3D]Hon: Give Iyo back![3C][3D]Taru: Give her back![33][3F]",
    # 43
    "Jii: Tako day again...[3C][3D]Taru: Yay! Hon: Taru![33][3F]",
    # 44
    "Hon: Found the present![3C][3D]Taru: Let's hurry![33][3F]",
    # 45
    "Taru: Lots of Tako![NL]Hon: Let's go![33][3F]",
    # 46
    "Hon: HBD Iyona![NL]Taru: HBD![3C][3D]Iyo: Thx guys![3C][3D]Come in! Let's party![33][3F]",
    # 47
    "Hon: No present yet![3C][3D]Taru: Let's look![33][3F]",
    # 48
    "Taru: Caught Iyona![NL]Iyo: Eek, Taru![3C][3D]Hon: Not fair![33][3F]",
    # 49
    "Ooaya: Goal![NL]Hon, what's wrong?[3C][3D]Hon: Barely made it...[33][3F]",
    # 50
    "Hon: Iyo! U safe?[3C][3D]Iyo: Hon! U saved me![3C][3D]Taru: Yeah![33][3F]",
    # 51
    "Taru: Matt, how's biz?[3C][3D]Hon: I want Tako![3C][3D]Matt: Free service![3C][3D][NL]Taru: Yay![33][3F]"
]

for i, group in enumerate(data):
    for j, entry in enumerate(group['Entries']):
        if i == 0:
            entry['TextTranslation'] = short_texts[j]

with open('JsonData/map_text.json', 'w', encoding='utf-8') as f:
    json.dump(data, f, indent=4, ensure_ascii=False)

print("Restored map_text.json to heavily compressed version and fixed MaxSize to 3992.")
