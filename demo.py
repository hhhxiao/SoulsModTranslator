import json, re


def replace(match):
    age = match.group()
    return "<" + age + ">"


if __name__ == "__main__":
    x = re.sub(r"[a-zA-Z]+", replace, "Helasdlo world!!?")
    print(x)
