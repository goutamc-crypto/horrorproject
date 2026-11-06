import bpy,sys,numpy as np
src,dst=sys.argv[sys.argv.index("--")+1:][:2]
img=bpy.data.images.load(src); px=np.array(img.pixels[:]).reshape(-1,4)
L=px[:,:3]@np.array([0.299,0.587,0.114]); L=(L-L.mean())*1.1+0.72  # boost contrast around mid
px[:,:3]=np.clip(L[:,None]*np.array([1.22,0.8,0.45]),0,1)        # warm Rajasthani sandstone
img.pixels[:]=px.ravel(); img.filepath_raw=dst; img.file_format='JPEG'; img.save()
