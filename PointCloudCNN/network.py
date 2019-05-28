import argparse
import re
import os, glob
import math
import random
import keras.losses
from keras.layers import Conv2D, MaxPool2D, Flatten, Dense
from keras.models import Sequential, load_model
from keras.callbacks import CSVLogger, ModelCheckpoint, LearningRateScheduler
from keras.optimizers import Adam
from tensorflow.python.keras.callbacks import TensorBoard
from skimage.io import imread
import numpy as np
from time import time

# Hyper parameter args
parser = argparse.ArgumentParser()

# Hyperparameters
parser.add_argument('--batch_size', default=64, type=int, help='batch size')
parser.add_argument('--scale_levels', default=5, type=int, help='nr. of scales in textures')
parser.add_argument('--M', default=33, type=int, help='noise level')
parser.add_argument('--epoch', default=50, type=int, help='number of train epochs')
parser.add_argument('--epoch_steps', default=100, type=int, help='steps per epoch')
parser.add_argument('--lr', default=1e-3, type=float, help='initial learning rate for Adam')

args = parser.parse_args()


# Create sequential CNN model as described in the paper
def cnn_model():
    model = Sequential()

    # add model layers
    model.add(
        Conv2D(50, kernel_size=3, activation='relu', input_shape=(args.M, args.M, args.scale_levels), name='input'))
    model.add(Conv2D(50, kernel_size=3, activation='relu'))

    model.add(MaxPool2D(pool_size=(2, 2)))

    model.add(Conv2D(96, kernel_size=3, activation='relu'))
    model.add(MaxPool2D(pool_size=(2, 2)))

    model.add(Flatten())  # 3456 neurons at this point
    model.add(Dense(2048, activation='relu'))
    model.add(Dense(1024, activation='relu'))
    model.add(Dense(512, activation='relu'))

    # Predict normal XY, roughness
    model.add(Dense(3, name='output'))

    return model


# Step schedule learning rate
def lr_schedule(epoch):
    drop = 0.5
    epochs_drop = 5
    return args.lr * math.pow(drop, math.floor((1 + epoch) / epochs_drop))


# Return one batch of data at a time for Keras
def train_datagen():
    index = 0
    train_data = './TrainingData/'
    files = os.listdir(train_data)
    files.sort()

    while True:
        batch_x = np.empty(shape=(args.batch_size, args.M, args.M, args.scale_levels))
        batch_y = np.empty(shape=(args.batch_size, 3))

        for i in range(args.batch_size):
            f = files[index]

            # Read of property of image from filename, bit of a hack
            x_index = f.index('_x_')
            y_index = f.index('_y_')
            r_index = f.index('_r_')

            ext_index = f.index('.png')

            normal_x = float(f[x_index + 3:y_index])
            normal_y = float(f[y_index + 3:r_index])
            roughness = float(f[r_index + 3:ext_index])

            # Read of image
            image = imread(train_data + f, as_gray=True)

            # Write to current abtch data
            for k in range(args.scale_levels):
                offset = k * args.M
                offset2 = (k + 1) * args.M

                batch_x[i, :, :, k] = image[offset:offset2, :]

            batch_y[i, :] = np.array([normal_x, normal_y, roughness])

            index += 1
            index = index % len(files)

        # Jump to a random point in the training data
        index = random.randrange(len(files) - args.batch_size)

        yield batch_x, batch_y


# FInd last epoch saved in a folder
def get_last_epoch(save_dir: str):
    # load the last model if it exists
    file_list = glob.glob(os.path.join(save_dir, 'model_*.hdf5'))  # get name list of all .hdf5 files

    if file_list:
        epochs_exist = []
        for file_ in file_list:
            result = re.findall(".*model_(.*).hdf5.*", file_)
            epochs_exist.append(int(result[0]))

        return max(epochs_exist)

    return 0


# Load model at specific or last epoch
def load_last_model(save_dir, load_epoch=-1):
    if load_epoch == -1:
        load_epoch = get_last_epoch(save_dir)

    return load_model(os.path.join(save_dir, 'model_%03d.hdf5' % load_epoch), compile=False)


# Main training routine
def train_model():
    # TODO: Validation losses!!

    # Setup checkpoint folder
    save_dir = './models/'
    os.makedirs(save_dir, mode=0o777, exist_ok=True)

    initial_epoch = get_last_epoch(save_dir)

    # Resume training if we have a checkpoint
    if initial_epoch > 0:
        print('resuming by loading epoch %03d' % initial_epoch)
        model = load_last_model(save_dir)
    else:
        # Create fresh model
        model = cnn_model()

    model.compile(
        optimizer=Adam(lr=args.lr, decay=0.0),  # Optimize with Adam. Learning rate is set by schedule
        loss=keras.losses.mean_squared_error
    )

    # Save out model occasionally
    checkpointer = ModelCheckpoint(os.path.join(save_dir, 'model_{epoch:03d}.hdf5'), verbose=1, save_weights_only=False,
                                   period=1)

    # Keep loss log
    csv_logger = CSVLogger(os.path.join(save_dir, 'log.csv'), append=True, separator=',')
    lr_scheduler = LearningRateScheduler(lr_schedule)

    tensorboard = TensorBoard(log_dir="board_logs/{}".format(time()))

    # Train model
    model.fit_generator(train_datagen(),
                        steps_per_epoch=args.epoch_steps,
                        epochs=args.epoch,
                        verbose=1,
                        initial_epoch=initial_epoch,
                        callbacks=[checkpointer, csv_logger, lr_scheduler, tensorboard]
                        )


if __name__ == '__main__':
    # Train model with current hyperparameters
    train_model()
