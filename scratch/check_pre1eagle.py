import re

with open('Assets/Scenes/pre1eagle.unity', 'r', encoding='utf-8') as f:
    text = f.read()

# Disable ContainerManager in pre1eagle.unity
# Find MonoBehaviour of ContainerManager
# guid: 3e26f1391160aa54a973c3ef1622b559
text_modified = re.sub(
    r'(m_Script: \{fileID: 11500000, guid: 3e26f1391160aa54a973c3ef1622b559[^\}]*\})',
    r'\1', # keep script
    text
)

# In ContainerManager component, set m_Enabled: 0
cm_match = re.search(r'--- !u!114 &(\d+)[\s\S]*?guid: 3e26f1391160aa54a973c3ef1622b559[\s\S]*?(?=--- !u!|\Z)', text)
if cm_match:
    cm_block = cm_match.group(0)
    cm_block_disabled = re.sub(r'm_Enabled:\s*1', 'm_Enabled: 0', cm_block)
    text = text.replace(cm_block, cm_block_disabled)
    print("Disabled ContainerManager in pre1eagle.unity")

print("Checking containers in pre1eagle.unity...")
# How many container GameObjects exist?
container_names = re.findall(r'm_Name:\s*([^\r\n]+)', text)
ctr_count = sum(1 for n in container_names if 'ft)' in n or 'ft_' in n)
print(f"Total container objects found: {ctr_count}")

