# Simulate AplicarSobre text merge
pt = {"F11": {"title": "Titulo PT", "description": "Desc PT"}}
en = {"F11": {"title": "Title EN", "description": "Desc EN"}}
local = {"title": "Local PT", "titleEN": "Local EN", "description": "Local D", "descriptionEN": "Local DEN"}

def apply(frame, local, r_titleEn=None):
    d = dict(local)
    temPt = frame in pt
    temEn = frame in en
    if temPt:
        if pt[frame]["title"]: d["title"] = pt[frame]["title"]
        if pt[frame]["description"]: d["description"] = pt[frame]["description"]
    if temEn:
        if en[frame]["title"]: d["titleEN"] = en[frame]["title"]
        if en[frame]["description"]: d["descriptionEN"] = en[frame]["description"]
    if r_titleEn: d["titleEN"] = r_titleEn
    return d

d = apply("F11", local)
assert d["title"] == "Titulo PT" and d["titleEN"] == "Title EN", d
# board PT
assert (False and d["titleEN"] or d["title"]) == "Titulo PT"
# board EN  
ingles=True
t = d["titleEN"] if ingles and d["titleEN"] else d["title"]
assert t == "Title EN", t
# no remote en: keep local EN
en.clear()
d2 = apply("F11", local)
assert d2["title"] == "Titulo PT" and d2["titleEN"] == "Local EN", d2
print("simulation OK")
